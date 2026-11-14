import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useReducer } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { AssessmentDto, CaseDto, ComplaintDto, ProfileDto } from '../api/types'
import { AuthProvider } from '../auth/AuthProvider'
import { emptyTicket, initialWizardState, wizardReducer, type WizardState } from '../hooks/useWizard'
import { ResultStep } from './ResultStep'

const eligible: AssessmentDto = {
  verdict: 'Eligible',
  reason: '50% ceny zapłaconego biletu.',
  compensationAmount: 74.95,
  percent: 50,
  belowMinimum: false,
  priceRequired: false,
  refusalRisk: false,
}

const preset: WizardState = {
  ...initialWizardState,
  step: 'result',
  source: 'manual',
  ticket: {
    ...emptyTicket,
    carrierChoice: 'PKP Intercity',
    trainCategory: 'IC',
    fromStation: 'Kraków Główny',
    toStation: 'Warszawa Centralna',
    travelDate: '2026-11-01',
    price: '149,90',
  },
  delay: { incidentType: 'Delay', minutes: 120, isSeasonTicket: false },
  assessment: eligible,
}

const letter: ComplaintDto = {
  subject: 'Reklamacja – opóźnienie pociągu IC z 1.11.2026',
  body: 'Szanowni Państwo,\n\nwnoszę o wypłatę odszkodowania.\n\nZ poważaniem\n[imię i nazwisko]\n[adres]',
  usedAi: true,
  missingFields: ['imię i nazwisko', 'adres'],
}

const IBAN = 'PL61109010140000071219812874'

let signedIn = false

function Harness({ initial = preset, onOpenCases }: { initial?: WizardState; onOpenCases?: () => void }) {
  const [state, dispatch] = useReducer(wizardReducer, initial)
  return (
    <>
      <p data-testid="step">{state.step}</p>
      {state.step === 'result' && <ResultStep state={state} dispatch={dispatch} onOpenCases={onOpenCases} />}
    </>
  )
}

const renderResult = (props: Parameters<typeof Harness>[0] = {}) =>
  render(
    <AuthProvider>
      <Harness {...props} />
    </AuthProvider>,
  )

const profile = (patch: Partial<ProfileDto> = {}): ProfileDto => ({
  email: 'jan@example.com',
  fullName: null,
  address: null,
  iban: null,
  preferredPayout: 'OriginalPayment',
  ...patch,
})

beforeEach(() => {
  signedIn = false
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('reduce'),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }))
  vi.spyOn(api, 'me').mockImplementation(async () => {
    if (!signedIn) throw new ApiError(401, 'Zaloguj się.')
    return { email: 'jan@example.com', isEmailConfirmed: true }
  })
  vi.spyOn(api, 'login').mockImplementation(async () => {
    signedIn = true
  })
  vi.spyOn(api, 'register').mockResolvedValue(undefined)
  vi.spyOn(api, 'getProfile').mockResolvedValue(profile({ preferredPayout: 'Voucher' }))
  vi.spyOn(api, 'createCase').mockResolvedValue({ id: 17 } as CaseDto)
  vi.spyOn(api, 'generateComplaint').mockResolvedValue(letter)
})

const saveButton = () => screen.getByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' })

describe('ResultStep – verdicts', () => {
  it('offers no save or letter for a train that is not covered, and says what to do instead', async () => {
    renderResult({
      initial: {
        ...preset,
        ticket: { ...preset.ticket, carrierChoice: 'Polregio', trainCategory: '' },
        assessment: { ...eligible, verdict: 'NotCovered', compensationAmount: 0, percent: 0, reason: 'Pociągi regionalne nie są objęte.' },
      },
    })
    expect(await screen.findByRole('heading', { name: 'Nie należy się' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Zapisz sprawę/ })).toBeNull()
    expect(screen.queryByRole('radiogroup')).toBeNull()
    expect(screen.getByText(/nie wypłaca odszkodowań wg przepisów UE/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Sprawdź kolejny bilet' })).toBeTruthy()
  })

  it('offers no save for "Do sprawdzenia" either', async () => {
    renderResult({
      initial: {
        ...preset,
        delay: { ...preset.delay, isSeasonTicket: true },
        assessment: { ...eligible, verdict: 'Unknown', compensationAmount: 0, reason: 'Bilety okresowe wymagają osobnego wyliczenia.' },
      },
    })
    expect(await screen.findByRole('heading', { name: 'Do sprawdzenia' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Zapisz sprawę/ })).toBeNull()
  })
})

describe('ResultStep – payout', () => {
  it('defaults to the original payment and shows the account number only for a transfer', async () => {
    const user = userEvent.setup()
    renderResult()
    const choice = await screen.findByRole('radiogroup', { name: /Jak chcesz otrzymać pieniądze/ })
    expect((within(choice).getByRole('radio', { name: /Zwrot na konto\/kartę użyte do zakupu/ }) as HTMLInputElement).checked).toBe(true)
    expect(screen.getByText(/Kupiłeś bilet gotówką w kasie\? Wybierz przelew\./)).toBeTruthy()
    expect(screen.queryByLabelText(/^Numer rachunku/)).toBeNull()

    await user.click(within(choice).getByRole('radio', { name: /Przelew na inny rachunek/ }))
    expect(screen.getByLabelText(/^Numer rachunku/)).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: 'Zapamiętaj na koncie' })).toBeTruthy()

    await user.click(within(choice).getByRole('radio', { name: /Bon/ }))
    expect(screen.queryByLabelText(/^Numer rachunku/)).toBeNull()
  })

  it("preselects the signed-in user's preferred payout and account number", async () => {
    signedIn = true
    vi.mocked(api.getProfile).mockResolvedValue(profile({ preferredPayout: 'BankTransfer', iban: IBAN }))
    renderResult()
    await waitFor(() => expect((screen.getByRole('radio', { name: /Przelew na inny rachunek/ }) as HTMLInputElement).checked).toBe(true))
    expect((screen.getByLabelText(/^Numer rachunku/) as HTMLInputElement).value).toBe(IBAN)
  })

  it('blocks saving with an invalid account number', async () => {
    const user = userEvent.setup()
    signedIn = true
    renderResult()
    await waitFor(() => expect((screen.getByRole('radio', { name: /Bon/ }) as HTMLInputElement).checked).toBe(true))
    await user.click(screen.getByRole('radio', { name: /Przelew na inny rachunek/ }))
    await user.type(screen.getByLabelText(/^Numer rachunku/), 'PL12 3456')
    await user.click(saveButton())
    expect((await screen.findByRole('alert')).textContent).toMatch(/numer rachunku/i)
    expect(api.createCase).not.toHaveBeenCalled()
  })
})

describe('ResultStep – save with inline sign-in', () => {
  it('asks a guest to sign in, then saves the case and prepares the letter without losing anything', async () => {
    const user = userEvent.setup()
    renderResult()

    await user.click(await screen.findByRole('radio', { name: /Przelew na inny rachunek/ }))
    await user.type(screen.getByLabelText(/^Numer rachunku/), IBAN)
    await user.click(screen.getByRole('checkbox', { name: 'Zapamiętaj na koncie' }))
    await user.click(saveButton())

    const panel = await screen.findByRole('region', { name: /Zaloguj się, aby zapisać sprawę/ })
    expect(api.createCase).not.toHaveBeenCalled()
    expect(within(panel).getByRole('tab', { name: 'Logowanie', selected: true })).toBeTruthy()

    await user.type(within(panel).getByLabelText('E-mail'), 'jan@example.com')
    await user.type(within(panel).getByLabelText('Hasło'), 'Haslo123!')
    await user.click(within(panel).getByRole('button', { name: 'Zaloguj się' }))

    await waitFor(() => expect(api.generateComplaint).toHaveBeenCalled())
    expect(api.login).toHaveBeenCalledWith('jan@example.com', 'Haslo123!')
    expect(vi.mocked(api.createCase).mock.calls[0][0]).toEqual({
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      fromStation: 'Kraków Główny',
      toStation: 'Warszawa Centralna',
      ticketNumber: null,
      travelDate: '2026-11-01',
      ticketPrice: 149.9,
      incidentType: 'Delay',
      delayMinutes: 120,
      delayCause: null,
      isSeasonTicket: false,
      payoutPreference: 'BankTransfer',
      notes: null,
    })
    expect(vi.mocked(api.generateComplaint).mock.calls[0].slice(0, 2)).toEqual([
      17,
      { payout: 'BankTransfer', oneTimeIban: IBAN, saveIbanToProfile: true },
    ])

    expect(await screen.findByRole('heading', { name: /Pismo jest gotowe/ })).toBeTruthy()
    expect(screen.getByRole('status', { name: /Uzupełnij w piśmie/ }).textContent).toMatch(/Uzupełnij w piśmie: imię i nazwisko, adres/)
    expect(screen.getByText(letter.subject)).toBeTruthy()
  })

  it('registers a new account from the same panel and continues', async () => {
    const user = userEvent.setup()
    renderResult()
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    const panel = await screen.findByRole('region', { name: /Zaloguj się, aby zapisać sprawę/ })
    await user.click(within(panel).getByRole('tab', { name: 'Rejestracja' }))
    await user.type(within(panel).getByLabelText('E-mail'), 'nowy@example.com')
    await user.type(within(panel).getByLabelText('Hasło'), 'Haslo123!')
    await user.click(within(panel).getByRole('button', { name: 'Załóż konto' }))

    await waitFor(() => expect(api.generateComplaint).toHaveBeenCalled())
    expect(api.register).toHaveBeenCalledWith('nowy@example.com', 'Haslo123!')
    expect(vi.mocked(api.generateComplaint).mock.calls[0][1]).toEqual({ payout: 'OriginalPayment', oneTimeIban: null, saveIbanToProfile: false })
  })

  it('shows sign-in errors in Polish and keeps the panel', async () => {
    const user = userEvent.setup()
    vi.mocked(api.login).mockRejectedValue(new ApiError(401, 'Nieprawidłowy e-mail lub hasło.'))
    renderResult()
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    const panel = await screen.findByRole('region', { name: /Zaloguj się, aby zapisać sprawę/ })
    await user.type(within(panel).getByLabelText('E-mail'), 'jan@example.com')
    await user.type(within(panel).getByLabelText('Hasło'), 'zle')
    await user.click(within(panel).getByRole('button', { name: 'Zaloguj się' }))
    expect((await within(panel).findByRole('alert')).textContent).toMatch(/Nieprawidłowy e-mail lub hasło/)
    expect(api.createCase).not.toHaveBeenCalled()
  })

  it('saves straight away for a signed-in user', async () => {
    const user = userEvent.setup()
    signedIn = true
    renderResult()
    await waitFor(() => expect(api.getProfile).toHaveBeenCalled())
    await user.click(saveButton())
    expect(await screen.findByRole('heading', { name: /Pismo jest gotowe/ })).toBeTruthy()
    expect(screen.queryByRole('region', { name: /Zaloguj się/ })).toBeNull()
  })
})

describe('ResultStep – errors', () => {
  it('explains a rejected travel date and leads back to the ticket', async () => {
    const user = userEvent.setup()
    signedIn = true
    vi.mocked(api.createCase).mockRejectedValue(
      new ApiError(400, 'Data podróży nie może być z przyszłości.', { travelDate: ['Data podróży nie może być z przyszłości.'] }),
    )
    renderResult()
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    expect((await screen.findByRole('alert')).textContent).toMatch(/Data podróży nie może być z przyszłości/)
    await user.click(screen.getByRole('button', { name: 'Popraw dane biletu' }))
    expect(screen.getByTestId('step').textContent).toBe('ticket')
  })

  it('says kindly when the case does not qualify (409)', async () => {
    const user = userEvent.setup()
    signedIn = true
    vi.mocked(api.generateComplaint).mockRejectedValue(new ApiError(409, 'Ta sprawa nie kwalifikuje się do odszkodowania – nie można wygenerować pisma.'))
    renderResult()
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    expect((await screen.findByRole('alert')).textContent).toMatch(/nie kwalifikuje się/)
    expect(screen.queryByRole('button', { name: 'Spróbuj ponownie' })).toBeNull()
  })

  it('offers a retry when the letter could not be prepared (502) without saving the case twice', async () => {
    const user = userEvent.setup()
    signedIn = true
    vi.mocked(api.generateComplaint).mockRejectedValueOnce(new ApiError(502, 'Usługa chwilowo niedostępna.'))
    renderResult()
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    await user.click(await screen.findByRole('button', { name: 'Spróbuj ponownie' }))
    expect(await screen.findByRole('heading', { name: /Pismo jest gotowe/ })).toBeTruthy()
    expect(api.createCase).toHaveBeenCalledTimes(1)
    expect(api.generateComplaint).toHaveBeenCalledTimes(2)
  })
})

describe('ResultStep – letter formats', () => {
  async function toLetter(onOpenCases?: () => void) {
    const user = userEvent.setup()
    signedIn = true
    renderResult({ onOpenCases })
    await user.click(await screen.findByRole('button', { name: 'Zapisz sprawę i przygotuj pismo' }))
    await screen.findByRole('heading', { name: /Pismo jest gotowe/ })
    return user
  }

  it('copies the subject and the body as text', async () => {
    const user = await toLetter()
    expect(screen.getByRole('radio', { name: 'Tekst', checked: true })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Kopiuj temat' }))
    expect(await navigator.clipboard.readText()).toBe(letter.subject)
    expect(await screen.findByText('Skopiowano')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Kopiuj treść' }))
    expect(await navigator.clipboard.readText()).toBe(letter.body)
  })

  it('downloads the PDF of exactly the shown text', async () => {
    const pdf = vi.spyOn(api, 'downloadComplaintPdf').mockResolvedValue(new Blob(['%PDF'], { type: 'application/pdf' }))
    const createUrl = vi.fn(() => 'blob:pdf')
    const revokeUrl = vi.fn()
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: createUrl, revokeObjectURL: revokeUrl }))
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const user = await toLetter()

    await user.click(screen.getByRole('radio', { name: 'PDF' }))
    await user.click(screen.getByRole('button', { name: 'Pobierz PDF' }))
    await waitFor(() => expect(click).toHaveBeenCalled())
    expect(pdf).toHaveBeenCalledWith(17, { subject: letter.subject, body: letter.body })
    const anchor = click.mock.contexts[0] as HTMLAnchorElement
    expect(anchor.download).toBe('reklamacja-17.pdf')
    await waitFor(() => expect(revokeUrl).toHaveBeenCalledWith('blob:pdf'), { timeout: 2000 })
  })

  it('leads to the cases list or to the next ticket', async () => {
    const onOpenCases = vi.fn()
    const user = await toLetter(onOpenCases)
    await user.click(screen.getByRole('button', { name: 'Przejdź do moich spraw' }))
    expect(onOpenCases).toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Sprawdź kolejny bilet' }))
    expect(screen.getByTestId('step').textContent).toBe('start')
  })
})
