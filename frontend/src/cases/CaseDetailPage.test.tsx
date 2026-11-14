import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { CaseDto } from '../api/types'
import { CaseDetailPage } from './CaseDetailPage'

const item: CaseDto = {
  id: 7,
  carrier: 'PKP Intercity',
  trainCategory: 'EIC',
  fromStation: 'Gdańsk Główny',
  toStation: 'Warszawa Centralna',
  ticketNumber: 'EIC-123',
  travelDate: '2026-10-20',
  ticketPrice: 200,
  incidentType: 'Delay',
  delayMinutes: 75,
  delayCause: null,
  isSeasonTicket: false,
  verdict: 'Eligible',
  refusalRisk: false,
  compensationAmount: 50,
  payoutPreference: 'Voucher',
  status: 'New',
  filedAt: null,
  responseDueDate: null,
  notes: null,
  createdAt: '2026-10-21T08:00:00Z',
  outcome: {
    title: 'Przewoźnik jest zobowiązany do wypłaty odszkodowania',
    statement: 'Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości 25% ceny biletu, tj. 50,00 zł.',
    legalBasis: 'Art. 19 rozporządzenia (UE) 2021/782',
    deadline: 'Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.',
  },
}

const renderAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/sprawy/:id" element={<CaseDetailPage />} />
        <Route path="/sprawy" element={<p>lista spraw</p>} />
      </Routes>
    </MemoryRouter>,
  )

beforeEach(() => {
  vi.spyOn(api, 'getCase').mockResolvedValue(item)
})

describe('CaseDetailPage', () => {
  it('shows the case and prepares the letter again from the saved case', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'generateComplaint').mockResolvedValue({
      subject: 'Reklamacja',
      body: 'Szanowni Państwo,\nproszę o bon.',
      usedAi: false,
      missingFields: [],
      deadlineStarted: false,
      responseDueDate: null,
    })
    renderAt('/sprawy/7')
    expect(await screen.findByRole('heading', { level: 1, name: 'PKP Intercity EIC' })).toBeTruthy()
    expect(screen.getByText('Gdańsk Główny → Warszawa Centralna')).toBeTruthy()
    expect(screen.getByText('Opóźnienie 75 min')).toBeTruthy()
    const outcome = screen.getByRole('region', { name: 'Werdykt końcowy' })
    expect(screen.getByRole('heading', { level: 2, name: 'Werdykt końcowy' })).toBeTruthy()
    expect(outcome.textContent).toContain('Przewoźnik jest zobowiązany do wypłaty odszkodowania')
    expect(outcome.textContent).toContain('tj. 50,00 zł.')

    await user.click(screen.getByRole('button', { name: 'Przygotuj pismo ponownie' }))
    await waitFor(() =>
      expect(api.generateComplaint).toHaveBeenCalledWith(
        7,
        { payout: 'Voucher', oneTimeIban: null, saveIbanToProfile: false },
        expect.any(AbortSignal),
      ),
    )
    expect(await screen.findByText(/proszę o bon/)).toBeTruthy()
  })

  it('prepares the complaint again for a regional train and for a covered train with nothing due', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'getCase').mockResolvedValueOnce({ ...item, carrier: 'Polregio', trainCategory: null, verdict: 'NotCovered', compensationAmount: 0 })
    vi.spyOn(api, 'generateComplaint').mockResolvedValue({ subject: 'Reklamacja', body: 'wg regulaminu', usedAi: false, missingFields: [], deadlineStarted: false, responseDueDate: null })
    const { unmount } = renderAt('/sprawy/7')
    await user.click(await screen.findByRole('button', { name: 'Przygotuj reklamację ponownie' }))
    expect(await screen.findByText(/wg regulaminu/)).toBeTruthy()
    unmount()

    vi.spyOn(api, 'getCase').mockResolvedValueOnce({ ...item, delayMinutes: 45, compensationAmount: 0 })
    renderAt('/sprawy/7')
    await screen.findByText('Opóźnienie 45 min')
    expect(screen.getByRole('button', { name: 'Przygotuj reklamację ponownie' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Przygotuj pismo ponownie' })).toBeNull()
  })

  it('starts the response clock when the first letter of a new case is generated', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'generateComplaint').mockResolvedValue({
      subject: 'Reklamacja',
      body: 'Treść',
      usedAi: false,
      missingFields: [],
      deadlineStarted: true,
      responseDueDate: '2026-12-13T10:00:00Z',
    })
    vi.mocked(api.getCase)
      .mockResolvedValueOnce(item)
      .mockResolvedValueOnce({ ...item, status: 'Sent', filedAt: '2026-11-13T10:00:00Z', responseDueDate: '2026-12-13T10:00:00Z' })
    renderAt('/sprawy/7')

    await user.click(await screen.findByRole('button', { name: 'Przygotuj pismo ponownie' }))

    expect(
      await screen.findByText(
        'Zakładamy, że wyślesz pismo dziś, więc termin odpowiedzi przewoźnika (do 13 grudnia 2026) liczymy od dzisiaj. Status możesz zmienić w Moich sprawach.',
      ),
    ).toBeTruthy()
    expect(await screen.findByText(/Odpowiedź do 13 grudnia 2026/)).toBeTruthy()
    expect(api.getCase).toHaveBeenCalledTimes(2)
  })

  it('does not claim a new deadline when the case was already sent', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'generateComplaint').mockResolvedValue({
      subject: 'Reklamacja',
      body: 'Treść ponownie',
      usedAi: false,
      missingFields: [],
      deadlineStarted: false,
      responseDueDate: '2026-11-20T10:00:00Z',
    })
    renderAt('/sprawy/7')

    await user.click(await screen.findByRole('button', { name: 'Przygotuj pismo ponownie' }))

    expect(await screen.findByText('Treść ponownie')).toBeTruthy()
    expect(screen.queryByText(/Zakładamy, że wyślesz pismo dziś/)).toBeNull()
    expect(api.getCase).toHaveBeenCalledTimes(1)
  })

  it('keeps focus on the page heading when the case finishes loading', async () => {
    let resolve!: (value: CaseDto) => void
    vi.mocked(api.getCase).mockReturnValue(new Promise((res) => (resolve = res)))
    renderAt('/sprawy/7')
    const heading = screen.getByRole('heading', { level: 1, name: 'Sprawa' })
    heading.focus()
    resolve(item)
    await screen.findByRole('heading', { level: 1, name: 'PKP Intercity EIC' })
    expect(document.activeElement).toBe(heading)
  })

  it('says when the case does not exist', async () => {
    vi.mocked(api.getCase).mockRejectedValue(new ApiError(404, 'Nie znaleziono.'))
    renderAt('/sprawy/99')
    expect(await screen.findByRole('heading', { level: 1, name: 'Nie znaleziono sprawy' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Wróć do listy spraw' })).toBeTruthy()
  })

  it('goes back to the list after deleting', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'deleteCase').mockResolvedValue(undefined)
    renderAt('/sprawy/7')
    await user.click(await screen.findByRole('button', { name: 'Usuń sprawę' }))
    await user.click(screen.getAllByRole('button', { name: 'Usuń sprawę' }).at(-1)!)
    expect(await screen.findByText('lista spraw')).toBeTruthy()
    expect(api.deleteCase).toHaveBeenCalledWith(7)
  })
})
