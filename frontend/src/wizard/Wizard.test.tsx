import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { AssessmentDto, AssessmentRequest, ExtractionDto, ReferenceDataDto } from '../api/types'
import { AuthProvider } from '../auth/AuthProvider'
import { Wizard } from './Wizard'

const renderWizard = () =>
  render(
    <AuthProvider>
      <Wizard />
    </AuthProvider>,
  )

const reference: ReferenceDataDto = {
  carriers: [
    { name: 'PKP Intercity', coverage: 'Eligible', categories: ['EIP', 'EIC', 'IC', 'TLK'] },
    { name: 'Polregio', coverage: 'NotCovered', categories: [] },
  ],
  categories: [
    { code: 'EIP', label: 'Express InterCity Premium' },
    { code: 'EIC', label: 'Express InterCity' },
    { code: 'IC', label: 'InterCity' },
    { code: 'TLK', label: 'Twoje Linie Kolejowe' },
  ],
  minimumPayout: 16,
}

const eligible = (request: AssessmentRequest): AssessmentDto => {
  const percent = request.delayMinutes >= 120 ? 50 : request.delayMinutes >= 60 ? 25 : 0
  return {
    verdict: 'Eligible',
    reason: `${percent}% ceny zapłaconego biletu.`,
    compensationAmount: percent === 50 ? 24.95 : percent === 25 ? 12.48 : 0,
    percent,
    belowMinimum: false,
    priceRequired: false,
    refusalRisk: false,
  }
}

const ticketPhoto = () => new File([new Uint8Array(2048)], 'bilet.jpg', { type: 'image/jpeg' })

/** Text of everything an element's aria-describedby points at (hints, errors). */
function description(element: HTMLElement): string {
  return (element.getAttribute('aria-describedby') ?? '')
    .split(' ')
    .filter(Boolean)
    .map((id) => document.getElementById(id)?.textContent ?? '')
    .join(' ')
}

const nextButton = () => screen.getByRole('button', { name: 'Dalej' }) as HTMLButtonElement

async function uploadAndRead(user: UserEvent, container: HTMLElement) {
  const input = container.querySelector<HTMLInputElement>('input[type="file"]')
  if (!input) throw new Error('no file input')
  await user.upload(input, ticketPhoto())
  await user.click(screen.getByRole('button', { name: 'Odczytaj dane z biletu' }))
}

async function fillManualTicket(user: UserEvent) {
  await user.selectOptions(screen.getByLabelText('Przewoźnik'), 'PKP Intercity')
  await user.selectOptions(screen.getByLabelText(/^Kategoria pociągu/), 'IC')
  fireEvent.change(screen.getByLabelText('Data przejazdu'), { target: { value: '2025-06-01' } })
  await user.type(screen.getByLabelText('Cena biletu'), '49,90')
}

beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }))
  vi.spyOn(api, 'me').mockRejectedValue(new ApiError(401, 'Zaloguj się.'))
  vi.spyOn(api, 'getReference').mockResolvedValue(reference)
  vi.spyOn(api, 'assess').mockImplementation(async (request) => eligible(request))
})

describe('Wizard – ticket step', () => {
  it('shows all three steps and both ways to start', async () => {
    renderWizard()
    const steps = screen.getByRole('list', { name: 'Postęp' })
    expect(within(steps).getByText('Bilet')).toBeTruthy()
    expect(within(steps).getByText('Opóźnienie')).toBeTruthy()
    expect(within(steps).getByText('Wynik')).toBeTruthy()
    expect(screen.getByRole('button', { name: /Wgraj bilet/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Wpisz ręcznie/ })).toBeTruthy()
  })


  it('has one page heading and limits text inputs to what the API accepts', async () => {
    const user = userEvent.setup()
    renderWizard()
    expect(screen.getByRole('heading', { level: 1, name: 'Sprawdź odszkodowanie za przejazd' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    expect((await screen.findByLabelText(/^Stacja początkowa/)).getAttribute('maxlength')).toBe('200')
    expect(screen.getByLabelText(/^Numer biletu/).getAttribute('maxlength')).toBe('100')
    expect(screen.getByRole('heading', { level: 2, name: 'Dane z biletu' })).toBeTruthy()
  })

  it('switches to manual entry with a message on 502 and keeps what was typed', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'extractTicket').mockRejectedValue(new ApiError(502, 'Model AI nie odpowiedział.'))
    const { container } = renderWizard()

    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await user.selectOptions(await screen.findByLabelText('Przewoźnik'), 'PKP Intercity')
    await user.type(screen.getByLabelText('Cena biletu'), '49,90')

    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)

    const notice = await screen.findByRole('status', { name: /Wpisz dane ręcznie/ })
    expect(notice.textContent).toMatch(/niedostępn/)
    expect((screen.getByLabelText('Przewoźnik') as HTMLSelectElement).value).toBe('PKP Intercity')
    expect((screen.getByLabelText('Cena biletu') as HTMLInputElement).value).toBe('49,90')
    expect(container.querySelector('input[type="file"]')).toBeNull()
  })

  it('switches to manual entry when AI is disabled (409) or rate limited (429)', async () => {
    const user = userEvent.setup()
    const extract = vi.spyOn(api, 'extractTicket').mockRejectedValueOnce(new ApiError(409, 'AI wyłączone – wpisz dane ręcznie.'))
    const { container } = renderWizard()

    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)
    expect((await screen.findByRole('status', { name: /Wpisz dane ręcznie/ })).textContent).toMatch(/wyłączony/)
    expect(screen.getByLabelText('Przewoźnik')).toBeTruthy()

    extract.mockRejectedValueOnce(new ApiError(429, 'Zbyt wiele zapytań.'))
    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await user.click(screen.getByRole('button', { name: 'Odczytaj dane z biletu' }))
    expect((await screen.findByRole('status', { name: /Wpisz dane ręcznie/ })).textContent).toMatch(/Spróbuj ponownie za minutę/)
  })

  it('fills the form from the ticket and highlights what AI could not read', async () => {
    const user = userEvent.setup()
    const extraction: ExtractionDto = {
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      fromStation: 'Kraków Główny',
      toStation: null,
      ticketNumber: null,
      ticketPrice: null,
      travelDate: null,
      missingFields: ['toStation', 'ticketNumber', 'ticketPrice', 'travelDate'],
      usedAi: true,
    }
    vi.spyOn(api, 'extractTicket').mockResolvedValue(extraction)
    const { container } = renderWizard()

    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)

    const price = await screen.findByLabelText('Cena biletu')
    expect((screen.getByLabelText('Przewoźnik') as HTMLSelectElement).value).toBe('PKP Intercity')
    expect((screen.getByLabelText(/^Stacja początkowa/) as HTMLInputElement).value).toBe('Kraków Główny')
    const missingHint = 'Uzupełnij – nie udało się odczytać'
    expect(description(price)).toContain(missingHint)
    expect(description(screen.getByLabelText('Data przejazdu'))).toContain(missingHint)
    expect(description(screen.getByLabelText(/^Stacja docelowa/))).toContain(missingHint)
    expect(description(screen.getByLabelText('Przewoźnik'))).not.toContain(missingHint)
    expect(nextButton().disabled).toBe(true)

    fireEvent.change(screen.getByLabelText('Data przejazdu'), { target: { value: '2025-06-01' } })
    await user.type(price, '120')

    // Stations and ticket number stay optional.
    expect(nextButton().disabled).toBe(false)
    expect(description(price)).not.toContain(missingHint)
  })

  it('names the fields it could not read and offers another photo', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'extractTicket').mockResolvedValue({
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      fromStation: 'Kraków Główny',
      toStation: 'Warszawa Centralna',
      ticketNumber: 'IC-1',
      ticketPrice: null,
      travelDate: null,
      missingFields: ['ticketPrice', 'travelDate'],
      usedAi: true,
    })
    const { container } = renderWizard()
    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)

    const notice = await screen.findByRole('status', { name: /Nie udało się odczytać/ })
    expect(notice.textContent).toContain('Nie udało się odczytać: data przejazdu i cena biletu.')
    expect(notice.textContent).toContain('Wgraj wyraźniejsze zdjęcie albo uzupełnij te pola ręcznie.')
    expect(within(notice).getByRole('button', { name: 'Wgraj inne zdjęcie' })).toBeTruthy()
    // Field-level highlights stay.
    expect(description(screen.getByLabelText('Cena biletu'))).toContain('Uzupełnij – nie udało się odczytać')
  })

  it('says plainly when nothing useful could be read', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'extractTicket').mockResolvedValue({
      carrier: null,
      trainCategory: null,
      fromStation: null,
      toStation: null,
      ticketNumber: null,
      ticketPrice: null,
      travelDate: null,
      missingFields: ['carrier', 'trainCategory', 'fromStation', 'toStation', 'ticketNumber', 'ticketPrice', 'travelDate'],
      usedAi: true,
    })
    const { container } = renderWizard()
    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)

    const notice = await screen.findByRole('status', { name: /Nie udało się odczytać biletu/ })
    expect(notice.textContent).toContain('Nie udało się odczytać biletu – zdjęcie może być nieostre lub ucięte.')
    expect(notice.textContent).toContain('Wgraj wyraźniejsze zdjęcie albo wpisz dane ręcznie.')
    expect(notice.textContent).not.toContain('Nie udało się odczytać:')
    expect(within(notice).getByRole('button', { name: 'Wgraj inne zdjęcie' })).toBeTruthy()
  })

  it('"Wgraj inne zdjęcie" keeps what was read and typed; a new read only fills the rest', async () => {
    const user = userEvent.setup()
    const extract = vi.spyOn(api, 'extractTicket').mockResolvedValueOnce({
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      fromStation: 'Kraków Główny',
      toStation: null,
      ticketNumber: null,
      ticketPrice: null,
      travelDate: null,
      missingFields: ['toStation', 'ticketNumber', 'ticketPrice', 'travelDate'],
      usedAi: true,
    })
    const { container } = renderWizard()
    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)
    await user.type(await screen.findByLabelText('Cena biletu'), '49,90')

    await user.click(screen.getByRole('button', { name: 'Wgraj inne zdjęcie' }))
    expect(container.querySelector('input[type="file"]')).not.toBeNull()

    extract.mockResolvedValueOnce({
      carrier: 'PKP Intercity',
      trainCategory: 'TLK',
      fromStation: 'Kraków Główny',
      toStation: 'Warszawa Centralna',
      ticketNumber: null,
      ticketPrice: 99,
      travelDate: '2025-06-01',
      missingFields: ['ticketNumber'],
      usedAi: true,
    })
    await uploadAndRead(user, container)

    expect(((await screen.findByLabelText('Cena biletu')) as HTMLInputElement).value).toBe('49,90')
    expect((screen.getByLabelText('Data przejazdu') as HTMLInputElement).value).toBe('2025-06-01')
    expect((screen.getByLabelText(/^Stacja docelowa/) as HTMLInputElement).value).toBe('Warszawa Centralna')
    expect((screen.getByLabelText(/^Kategoria pociągu/) as HTMLSelectElement).value).toBe('TLK')
    expect(screen.getByRole('status', { name: /Nie udało się odczytać/ }).textContent).toContain('numer biletu')
  })

  it('shows at once that a regional carrier is outside the EU 25% / 50% rules, without blocking', async () => {
    const user = userEvent.setup()
    renderWizard()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await user.selectOptions(await screen.findByLabelText('Przewoźnik'), 'Polregio')

    const carrier = screen.getByLabelText('Przewoźnik')
    expect(description(carrier)).toContain(
      'Polregio to przewoźnik regionalny. Przepisy UE o odszkodowaniu 25% / 50% go nie obejmują, więc nie wyliczymy kwoty. ' +
        'Możesz kontynuować i przygotować reklamację – przewoźnik rozpatrzy ją według swojego regulaminu.',
    )
    expect(carrier.getAttribute('aria-invalid')).not.toBe('true')
    expect(screen.queryByRole('alert')).toBeNull()

    fireEvent.change(screen.getByLabelText('Data przejazdu'), { target: { value: '2025-06-01' } })
    await user.type(screen.getByLabelText('Cena biletu'), '19,90')
    expect(nextButton().disabled).toBe(false)
  })

  it('marks an InterRegio train with another carrier as "Do sprawdzenia"', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'extractTicket').mockResolvedValue({
      carrier: 'Inny Przewoźnik',
      trainCategory: 'IR',
      fromStation: null,
      toStation: null,
      ticketNumber: null,
      ticketPrice: 30,
      travelDate: '2025-06-01',
      missingFields: [],
      usedAi: true,
    })
    const { container } = renderWizard()
    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)

    const category = await screen.findByLabelText(/^Kategoria pociągu/)
    expect(description(category)).toMatch(/^Do sprawdzenia/)
    expect(description(category)).toContain('przygotować reklamację')
    expect(nextButton().disabled).toBe(false)
  })

  it('blocks "Dalej" only while carrier, travel date or price are missing', async () => {
    const user = userEvent.setup()
    renderWizard()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await screen.findByLabelText('Przewoźnik')
    expect(nextButton().disabled).toBe(true)

    await user.selectOptions(screen.getByLabelText('Przewoźnik'), 'PKP Intercity')
    fireEvent.change(screen.getByLabelText('Data przejazdu'), { target: { value: '2025-06-01' } })
    expect(nextButton().disabled).toBe(true)

    await user.type(screen.getByLabelText('Cena biletu'), '49,90')
    expect(nextButton().disabled).toBe(false)

    await user.selectOptions(screen.getByLabelText('Przewoźnik'), 'Inny')
    expect(nextButton().disabled).toBe(true)
    await user.type(screen.getByLabelText('Nazwa przewoźnika'), 'Leo Express')
    expect(nextButton().disabled).toBe(false)

    fireEvent.change(screen.getByLabelText('Data przejazdu'), { target: { value: '2999-01-01' } })
    expect(nextButton().disabled).toBe(true)
  })
})

describe('Wizard – delay step', () => {
  it('re-assesses when minutes change, does not require a cause and keeps data when going back', async () => {
    const user = userEvent.setup()
    const assess = vi.mocked(api.assess)
    renderWizard()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await screen.findByLabelText('Przewoźnik')
    await fillManualTicket(user)
    await user.click(nextButton())

    const minutes = await screen.findByLabelText(/^Opóźnienie/)
    await user.type(minutes, '75')
    await waitFor(() =>
      expect(assess.mock.lastCall?.[0]).toEqual(
        expect.objectContaining({ carrier: 'PKP Intercity', trainCategory: 'IC', delayMinutes: 75, ticketPrice: 49.9, delayCause: null, incidentType: 'Delay' }),
      ),
    )
    expect(await screen.findByText(/12,48/)).toBeTruthy()

    await user.click(screen.getByRole('button', { name: '120 min' }))
    await waitFor(() => expect(assess.mock.lastCall?.[0]).toEqual(expect.objectContaining({ delayMinutes: 120 })))

    // No cause chosen – still allowed to see the result.
    const toResult = screen.getByRole('button', { name: 'Zobacz wynik' }) as HTMLButtonElement
    expect(toResult.disabled).toBe(false)
    await user.click(toResult)
    expect(await screen.findByText(/24,95/)).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'Wstecz' }))
    expect(((await screen.findByLabelText(/^Opóźnienie/)) as HTMLInputElement).value).toBe('120')
    await user.click(screen.getByRole('button', { name: 'Wstecz' }))
    expect(((await screen.findByLabelText('Cena biletu')) as HTMLInputElement).value).toBe('49,90')
  })

  it('hides minutes for a cancelled train and sends the cause when chosen', async () => {
    const user = userEvent.setup()
    const assess = vi.mocked(api.assess)
    renderWizard()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await screen.findByLabelText('Przewoźnik')
    await fillManualTicket(user)
    await user.click(nextButton())

    await user.click(await screen.findByRole('switch', { name: 'Pociąg odwołany' }))
    expect(screen.queryByLabelText(/^Opóźnienie/)).toBeNull()
    await user.selectOptions(screen.getByLabelText(/^Przyczyna opóźnienia/), 'Pogoda')
    await user.click(screen.getByRole('checkbox', { name: /Bilet okresowy/ }))

    await waitFor(() =>
      expect(assess.mock.lastCall?.[0]).toEqual(
        expect.objectContaining({ incidentType: 'Cancellation', delayCause: 'Weather', isSeasonTicket: true }),
      ),
    )
    // The cause can be cleared again.
    await user.selectOptions(screen.getByLabelText(/^Przyczyna opóźnienia/), '— nie wiem —')
    await waitFor(() => expect(assess.mock.lastCall?.[0]).toEqual(expect.objectContaining({ delayCause: null })))
  })
})

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => {
    resolve = done
  })
  return { promise, resolve }
}

const readTicket: ExtractionDto = {
  carrier: 'Polregio',
  trainCategory: null,
  fromStation: 'Gdynia Główna',
  toStation: null,
  ticketNumber: null,
  ticketPrice: 999,
  travelDate: '2025-01-01',
  missingFields: [],
  usedAi: true,
}

describe('Wizard – late answers and focus', () => {
  it('cancels a running read when the user switches to typing, and a late result changes nothing', async () => {
    const user = userEvent.setup()
    const pending = deferred<ExtractionDto>()
    const extract = vi.spyOn(api, 'extractTicket').mockReturnValue(pending.promise) // ignores the signal on purpose
    const { container } = renderWizard()

    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)
    // "Wpisz dane ręcznie" stays usable while reading – it is the escape hatch.
    await user.click(screen.getByRole('button', { name: 'Wpisz dane ręcznie' }))
    expect(extract.mock.calls[0][1]?.aborted).toBe(true)

    await user.selectOptions(await screen.findByLabelText('Przewoźnik'), 'PKP Intercity')
    await user.type(screen.getByLabelText('Cena biletu'), '49,90')

    pending.resolve(readTicket)
    await new Promise((done) => setTimeout(done, 0))

    expect((screen.getByLabelText('Przewoźnik') as HTMLSelectElement).value).toBe('PKP Intercity')
    expect((screen.getByLabelText('Cena biletu') as HTMLInputElement).value).toBe('49,90')
    expect((screen.getByLabelText(/^Stacja początkowa/) as HTMLInputElement).value).toBe('')
    expect(screen.getByRole('heading', { name: 'Dane z biletu' })).toBeTruthy()
  })

  it('drops an assessment that answers after the user went back and changed the ticket', async () => {
    const user = userEvent.setup()
    const late = deferred<AssessmentDto>()
    const assess = vi.mocked(api.assess)
    renderWizard()
    await user.click(screen.getByRole('button', { name: /Wpisz ręcznie/ }))
    await screen.findByLabelText('Przewoźnik')
    await fillManualTicket(user)
    await user.click(nextButton())

    assess.mockReturnValueOnce(late.promise)
    await user.type(await screen.findByLabelText(/^Opóźnienie/), '75')
    await waitFor(() => expect(assess).toHaveBeenCalledTimes(1))
    const signal = assess.mock.calls[0][1]

    await user.click(screen.getByRole('button', { name: 'Wstecz' }))
    expect(signal?.aborted).toBe(true)
    const price = await screen.findByLabelText('Cena biletu')
    await user.clear(price)
    await user.type(price, '80')

    late.resolve({ ...eligible({ ...assess.mock.calls[0][0], delayMinutes: 75 }), compensationAmount: 99.99 })
    await new Promise((done) => setTimeout(done, 0))

    await user.click(nextButton())
    await screen.findByLabelText(/^Opóźnienie/)
    expect(screen.queryByText(/99,99/)).toBeNull()
  })

  it('moves focus to the heading of the form after a read, whether it worked or not', async () => {
    const user = userEvent.setup()
    const extract = vi.spyOn(api, 'extractTicket').mockResolvedValueOnce(readTicket)
    const { container } = renderWizard()

    await user.click(screen.getByRole('button', { name: /Wgraj bilet/ }))
    await uploadAndRead(user, container)
    const reviewHeading = await screen.findByRole('heading', { name: 'Sprawdź dane z biletu' })
    expect(document.activeElement).toBe(reviewHeading)

    extract.mockRejectedValueOnce(new ApiError(502, 'AI niedostępne.'))
    await user.click(screen.getByRole('button', { name: 'Zmień pliki' }))
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'Dodaj bilet' }))
    await user.click(screen.getByRole('button', { name: 'Odczytaj dane z biletu' }))
    await screen.findByRole('status', { name: /Wpisz dane ręcznie/ })
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'Dane z biletu' }))
  })
})
