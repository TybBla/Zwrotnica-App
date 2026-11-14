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
  expenseDescription: null,
  expenseAmount: null,
  createdAt: '2026-10-21T08:00:00Z',
  outcome: {
    title: 'Przewoźnik jest zobowiązany do wypłaty odszkodowania',
    statement: 'Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości 25% ceny biletu, tj. 50,00 zł.',
    legalBasis: 'Art. 19 rozporządzenia (UE) 2021/782',
    deadline: 'Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.',
  },
  followUps: { reminder: false, utkComplaint: false, pastDeadline: false },
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
  vi.spyOn(api, 'getReference').mockResolvedValue({
    carriers: [
      { name: 'PKP Intercity', coverage: 'Eligible', categories: ['EIC'], complaintUrl: 'https://www.intercity.pl/reklamacje.html' },
    ],
    categories: [],
    minimumPayout: 16,
    utk: {
      name: 'Prezes Urzędu Transportu Kolejowego',
      complaintUrl: 'https://utk.gov.pl/pl/urzad/kontakt',
      complaintChannels: 'E-mailem na adres pasazer@utk.gov.pl.',
    },
  })
})

describe('CaseDetailPage', () => {
  it('shows the documented costs of a regional case', async () => {
    vi.mocked(api.getCase).mockResolvedValue({
      ...item,
      carrier: 'Polregio',
      trainCategory: null,
      verdict: 'NotCovered',
      compensationAmount: 0,
      expenseDescription: 'Taksówka do Stargardu',
      expenseAmount: 85,
    })
    renderAt('/sprawy/7')

    const costs = (await screen.findByText('Poniesione koszty')).closest('div')!
    expect(costs.textContent).toContain('85,00 zł')
    expect(costs.textContent).toContain('Taksówka do Stargardu')
    expect(screen.getByRole('button', { name: 'Przygotuj wniosek o zwrot kosztów ponownie' })).toBeTruthy()
  })

  it('shows the cost description even when no amount is stored', async () => {
    vi.mocked(api.getCase).mockResolvedValue({ ...item, expenseDescription: 'Taksówka do Stargardu', expenseAmount: null })
    renderAt('/sprawy/7')

    const costs = (await screen.findByText('Poniesione koszty')).closest('div')!
    expect(costs.textContent).toContain('Taksówka do Stargardu')
  })

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

describe('CaseDetailPage – next steps', () => {
  const sent: CaseDto = {
    ...item,
    status: 'Sent',
    filedAt: '2026-10-13T10:00:00Z',
    responseDueDate: '2026-11-13T10:00:00Z',
    followUps: { reminder: true, utkComplaint: true, pastDeadline: true },
  }
  const followUpLetter = (subject: string, body: string, missingFields: string[] = []) => ({
    subject,
    body,
    usedAi: false,
    missingFields,
    deadlineStarted: false,
    responseDueDate: null,
  })

  it('is hidden while the case allows no follow-up letter', async () => {
    renderAt('/sprawy/7')
    await screen.findByRole('heading', { level: 1, name: 'PKP Intercity EIC' })

    expect(screen.queryByRole('heading', { name: 'Kolejne kroki' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Przygotuj ponaglenie' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Dodaj do kalendarza' })).toBeNull()
  })

  it('offers both letters after the deadline and shows the complaint to UTK with its own recipient', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getCase).mockResolvedValue(sent)
    vi.spyOn(api, 'generateFollowUp').mockResolvedValue(
      followUpLetter('Skarga na przewoźnika PKP Intercity', 'Prezes Urzędu Transportu Kolejowego\n[kopia biletu]', [
        'załączniki – kopie pisma i biletu',
      ]),
    )
    vi.spyOn(api, 'downloadFollowUpPdf').mockResolvedValue(new Blob(['%PDF']))
    URL.createObjectURL = vi.fn(() => 'blob:pdf')
    URL.revokeObjectURL = vi.fn()
    renderAt('/sprawy/7')

    expect(await screen.findByRole('heading', { level: 2, name: 'Kolejne kroki' })).toBeTruthy()
    expect(screen.getByText(/Termin odpowiedzi przewoźnika minął/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Przygotuj ponaglenie' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Przygotuj skargę do UTK' }))

    expect(api.generateFollowUp).toHaveBeenCalledWith(7, 'utk-complaint', expect.any(AbortSignal))
    expect(await screen.findByRole('heading', { name: 'Skarga do UTK jest gotowa' })).toBeTruthy()
    expect(screen.getByText('Uzupełnij w piśmie: załączniki – kopie pisma i biletu')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Gdzie wysłać skargę' })).toBeTruthy()
    expect(await screen.findByText('E-mailem na adres pasazer@utk.gov.pl.')).toBeTruthy()

    await user.click(screen.getByRole('radio', { name: 'PDF' }))
    expect(screen.getByText('skarga-utk-7.pdf')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Pobierz PDF' }))
    expect(api.downloadFollowUpPdf).toHaveBeenCalledWith(7, 'utk-complaint', {
      subject: 'Skarga na przewoźnika PKP Intercity',
      body: 'Prezes Urzędu Transportu Kolejowego\n[kopia biletu]',
    })
  })

  it('asks to confirm a reminder while the carrier still has time', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getCase).mockResolvedValue({
      ...sent,
      responseDueDate: '2099-12-13T10:00:00Z',
      followUps: { reminder: true, utkComplaint: false, pastDeadline: false },
    })
    vi.spyOn(api, 'generateFollowUp').mockResolvedValue(
      followUpLetter('Ponaglenie – wniosek', 'do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi'),
    )
    renderAt('/sprawy/7')

    expect(await screen.findByText(/Przewoźnik ma czas na odpowiedź do 13 grudnia 2099/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Przygotuj skargę do UTK' })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Przygotuj ponaglenie' }))
    expect(api.generateFollowUp).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog', { name: 'Termin odpowiedzi jeszcze trwa' })).toBeTruthy()
    await user.click(screen.getAllByRole('button', { name: 'Przygotuj ponaglenie' }).at(-1)!)

    expect(await screen.findByRole('heading', { name: 'Ponaglenie jest gotowe' })).toBeTruthy()
    expect(api.generateFollowUp).toHaveBeenCalledWith(7, 'reminder', expect.any(AbortSignal))
    expect(screen.getByRole('heading', { name: 'Gdzie wysłać pismo' })).toBeTruthy()
  })

  it('offers only the complaint to UTK after a rejection and shows a 409 reason', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getCase).mockResolvedValue({
      ...sent,
      status: 'Rejected',
      followUps: { reminder: false, utkComplaint: true, pastDeadline: false },
    })
    vi.spyOn(api, 'generateFollowUp').mockRejectedValue(new ApiError(409, 'Skargę do UTK przygotujemy później.'))
    renderAt('/sprawy/7')

    expect(await screen.findByText(/Przewoźnik odrzucił pismo/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Przygotuj ponaglenie' })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Przygotuj skargę do UTK' }))
    expect(await screen.findByText('Skargę do UTK przygotujemy później.')).toBeTruthy()
  })

  it('adds the response deadline to the calendar as an .ics file', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getCase).mockResolvedValue({ ...sent, followUps: { reminder: true, utkComplaint: false, pastDeadline: false } })
    const created: Blob[] = []
    URL.createObjectURL = vi.fn((blob: Blob | MediaSource) => {
      created.push(blob as Blob)
      return 'blob:ics'
    })
    URL.revokeObjectURL = vi.fn()
    renderAt('/sprawy/7')

    await user.click(await screen.findByRole('button', { name: 'Dodaj do kalendarza' }))

    expect(created).toHaveLength(1)
    expect(created[0].type).toBe('text/calendar;charset=utf-8')
    const text = await created[0].text()
    expect(text).toContain('DTSTART;VALUE=DATE:20261113')
    expect(text).toContain('/sprawy/7')
  })

  it('reminds on the letter that Zwrotnica never asks for card details or payment', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'generateComplaint').mockResolvedValue(followUpLetter('Reklamacja', 'Treść'))
    renderAt('/sprawy/7')

    await user.click(await screen.findByRole('button', { name: 'Przygotuj pismo ponownie' }))

    expect(await screen.findByText(/nigdy nie prosi o dane karty ani o opłatę/)).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Gdzie wysłać pismo' })).toBeTruthy()
    expect(await screen.findByRole('link', { name: /intercity\.pl/ })).toBeTruthy()
  })
})
