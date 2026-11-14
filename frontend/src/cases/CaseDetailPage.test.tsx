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
    })
    renderAt('/sprawy/7')
    expect(await screen.findByRole('heading', { level: 1, name: 'PKP Intercity EIC' })).toBeTruthy()
    expect(screen.getByText('Gdańsk Główny → Warszawa Centralna')).toBeTruthy()
    expect(screen.getByText('Opóźnienie 75 min')).toBeTruthy()

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
