import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { CaseDto, StatsDto } from '../api/types'
import { CasesPage } from './CasesPage'

const base: CaseDto = {
  id: 1,
  carrier: 'PKP Intercity',
  trainCategory: 'IC',
  fromStation: 'Kraków Główny',
  toStation: 'Warszawa Centralna',
  ticketNumber: 'X1',
  travelDate: '2026-11-01',
  ticketPrice: 149.9,
  incidentType: 'Delay',
  delayMinutes: 120,
  delayCause: null,
  isSeasonTicket: false,
  verdict: 'Eligible',
  refusalRisk: false,
  compensationAmount: 74.95,
  payoutPreference: 'OriginalPayment',
  status: 'New',
  filedAt: null,
  responseDueDate: null,
  notes: null,
  createdAt: '2026-11-02T10:00:00Z',
  outcome: null,
}

const second: CaseDto = {
  ...base,
  id: 2,
  carrier: 'PKP Intercity',
  trainCategory: 'TLK',
  fromStation: null,
  toStation: null,
  compensationAmount: 20,
  status: 'Paid',
}

const stats: StatsDto = { recovered: 20, pending: 74.95, totalCases: 2 }

const renderPage = () =>
  render(
    <MemoryRouter>
      <CasesPage />
    </MemoryRouter>,
  )

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

beforeEach(() => {
  vi.spyOn(api, 'listCases').mockResolvedValue([base, second])
  vi.spyOn(api, 'getStats').mockResolvedValue(stats)
})

describe('CasesPage', () => {
  it('lists the cases as rows with a summary from the stats', async () => {
    renderPage()
    expect(screen.getByRole('heading', { level: 1, name: 'Moje sprawy' })).toBeTruthy()
    const summary = await screen.findByText(/Odzyskano/)
    expect(summary.closest('[data-summary]')?.textContent?.replace(/\s/g, ' ')).toMatch(
      /Odzyskano 20,00 zł · Czeka 74,95 zł · 2 sprawy/,
    )
    const rows = await screen.findAllByRole('listitem')
    expect(rows).toHaveLength(2)
    expect(within(rows[0]).getByText(/Kraków Główny → Warszawa Centralna/)).toBeTruthy()
    expect(within(rows[0]).getByText('Należy się')).toBeTruthy()
    expect(within(rows[1]).getByText('trasa nieznana')).toBeTruthy()
  })

  it('changes the status at once and rolls it back when the server refuses', async () => {
    const user = userEvent.setup()
    const pending = deferred<CaseDto>()
    vi.spyOn(api, 'setStatus').mockReturnValue(pending.promise)
    renderPage()
    const row = (await screen.findAllByRole('listitem'))[0]
    const select = within(row).getByRole('combobox', { name: /Status/ }) as HTMLSelectElement
    expect(select.value).toBe('New')

    await user.selectOptions(select, 'Sent')
    // Optimistic: shown before the server answers.
    expect(select.value).toBe('Sent')
    expect(api.setStatus).toHaveBeenCalledWith(1, 'Sent')

    pending.reject(new ApiError(500, 'Coś poszło nie tak po stronie serwera.'))
    await waitFor(() => expect(select.value).toBe('New'))
    expect(within(row).getByRole('alert').textContent).toMatch(/Nie udało się zmienić statusu/)
  })

  it('keeps the server answer after a successful change and refreshes the summary', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'setStatus').mockResolvedValue({
      ...base,
      status: 'Sent',
      filedAt: new Date().toISOString(),
      responseDueDate: new Date(Date.now() + 20 * 86_400_000).toISOString(),
    })
    renderPage()
    const row = (await screen.findAllByRole('listitem'))[0]
    await user.selectOptions(within(row).getByRole('combobox', { name: /Status/ }), 'Sent')
    expect(await within(row).findByText(/jeszcze \d+ dni/)).toBeTruthy()
    await waitFor(() => expect(api.getStats).toHaveBeenCalledTimes(2))
    expect(screen.getByText(/Status zmieniony: Wysłana/)).toBeTruthy()
  })

  it('keeps the latest choice when answers to two quick changes arrive out of order', async () => {
    const user = userEvent.setup()
    const toSent = deferred<CaseDto>()
    const toPaid = deferred<CaseDto>()
    vi.spyOn(api, 'setStatus').mockImplementation((_id, status) => (status === 'Sent' ? toSent.promise : toPaid.promise))
    const statsAfterPaid = deferred<StatsDto>()
    const statsAfterSent = deferred<StatsDto>()
    vi.mocked(api.getStats)
      .mockResolvedValueOnce(stats)
      .mockReturnValueOnce(statsAfterPaid.promise)
      .mockReturnValueOnce(statsAfterSent.promise)
    renderPage()
    const row = (await screen.findAllByRole('listitem'))[0]
    const select = within(row).getByRole('combobox', { name: /Status/ }) as HTMLSelectElement
    await user.selectOptions(select, 'Sent')
    await user.selectOptions(select, 'Paid')

    toPaid.resolve({ ...base, status: 'Paid' })
    await waitFor(() => expect(api.getStats).toHaveBeenCalledTimes(2))
    toSent.resolve({ ...base, status: 'Sent', responseDueDate: '2026-12-20T10:00:00Z' })
    await waitFor(() => expect(api.getStats).toHaveBeenCalledTimes(3))
    expect(select.value).toBe('Paid')

    // The newest summary wins even if the older request answers last.
    statsAfterSent.resolve({ recovered: 94.95, pending: 0, totalCases: 2 })
    expect(await screen.findByText('94,95 zł')).toBeTruthy()
    statsAfterPaid.resolve({ recovered: 1, pending: 1, totalCases: 2 })
    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(screen.getByText('94,95 zł')).toBeTruthy()
    expect(screen.queryByText('1,00 zł')).toBeNull()
  })

  it('deletes a case only after confirming in a dialog', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'deleteCase').mockResolvedValue(undefined)
    renderPage()
    const row = (await screen.findAllByRole('listitem'))[0]
    await user.click(within(row).getByRole('button', { name: /Usuń/ }))

    const dialog = screen.getByRole('dialog', { name: 'Usunąć sprawę?' })
    await user.click(within(dialog).getByRole('button', { name: 'Anuluj' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.deleteCase).not.toHaveBeenCalled()

    await user.click(within(row).getByRole('button', { name: /Usuń/ }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Usuń sprawę' }))
    await waitFor(() => expect(screen.getAllByRole('listitem')).toHaveLength(1))
    expect(api.deleteCase).toHaveBeenCalledWith(1)
  })

  it('shows an empty state that starts a check', async () => {
    vi.mocked(api.listCases).mockResolvedValue([])
    vi.mocked(api.getStats).mockResolvedValue({ recovered: 0, pending: 0, totalCases: 0 })
    renderPage()
    const cta = await screen.findByRole('link', { name: 'Sprawdź bilet' })
    expect(cta.getAttribute('href')).toBe('/sprawdz')
  })

  it('shows an error with a retry when the list cannot be loaded', async () => {
    const user = userEvent.setup()
    vi.mocked(api.listCases).mockRejectedValueOnce(new ApiError(0, 'Brak połączenia z serwerem.'))
    renderPage()
    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toMatch(/Brak połączenia/)
    await user.click(within(alert).getByRole('button', { name: 'Spróbuj ponownie' }))
    expect(await screen.findAllByRole('listitem')).toHaveLength(2)
  })
})
