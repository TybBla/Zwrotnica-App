import { useEffect, useId, useRef, useState, type Dispatch, type RefObject } from 'react'
import { ApiError, api } from '../api/client'
import type { CaseRequest, ComplaintDto, ComplaintRequest } from '../api/types'
import { AuthPanel } from '../auth/AuthPanel'
import { useAccountChange } from '../auth/useAccountChange'
import { useAuth } from '../auth/useAuth'
import { caseRoute } from '../cases/caseText'
import { Button } from '../components/Button'
import { AlertCircleIcon, ArrowRightIcon } from '../components/icons'
import {
  caseKey,
  toCaseRequest,
  toTicketFields,
  type PendingCase,
  type WizardAction,
  type WizardState,
  type WizardStep,
} from '../hooks/useWizard'
import { formatAmount, formatDate } from '../lib/format'
import { isValidIban, normalizeIban } from '../lib/textRules'
import { letterKind, shownVerdict, verdictStyle, type LetterKind } from '../lib/verdict'
import { FinalVerdict } from './FinalVerdict'
import { LetterChoice } from './LetterChoice'
import { PayoutChoice } from './PayoutChoice'
import { VerdictCard, type VerdictDetail } from './VerdictCard'
import { WizardActions } from './WizardActions'
import './Result.css'

/** The AI may take a while; past this the user gets a retry instead of an endless spinner. */
const LETTER_TIMEOUT_MS = 60_000
/** Saving the case is quick; past this the user gets a retry (the request itself keeps going, see PendingCase). */
const SAVE_TIMEOUT_MS = 20_000

class Timeout extends Error {}

/** Rejects with Timeout after `ms` without cancelling `promise`. */
function within<T>(promise: Promise<T>, ms: number): Promise<T> {
  let timer: number | undefined
  const limit = new Promise<never>((_, reject) => {
    timer = window.setTimeout(() => reject(new Timeout()), ms)
  })
  return Promise.race([promise, limit]).finally(() => window.clearTimeout(timer))
}

const isAbort = (error: unknown) => error instanceof DOMException && error.name === 'AbortError'

/**
 * 'auth': a guest pressed save – after signing in the save continues by itself.
 * 'reauth': the session expired mid-save – after signing in the user confirms the payout and saves again themselves.
 */
type Phase = 'idle' | 'auth' | 'reauth' | 'saving' | 'writing' | 'done'

const SESSION_EXPIRED = 'Sesja wygasła. Zaloguj się ponownie i potwierdź formę wypłaty.'

type SaveError =
  /** Something the user can fix in an earlier step (the API rejected a field). */
  | { kind: 'fix'; messages: string[]; step: Extract<WizardStep, 'ticket' | 'delay'> }
  /** The API refuses the letter (409, e.g. no ticket price): nothing to retry. */
  | { kind: 'final'; message: string }
  /** Temporary trouble (AI down, timeout, network): try again. */
  | { kind: 'retry'; message: string }

const delayFields = new Set(['incidentType', 'delayMinutes', 'delayCause', 'isSeasonTicket', 'expenseDescription', 'expenseAmount'])

const saveLabels: Record<LetterKind, string> = {
  compensation: 'Zapisz sprawę i przygotuj pismo',
  carrierRules: 'Zapisz sprawę i przygotuj reklamację',
  documentedCosts: 'Zapisz sprawę i przygotuj wniosek o zwrot kosztów',
}

interface ResultStepProps {
  state: WizardState
  dispatch: Dispatch<WizardAction>
  /** "Przejdź do moich spraw" (wired to the cases page by the router). */
  onOpenCases?: () => void
  /** Owned by Wizard, so a save sent before leaving the step is joined, not repeated, after coming back. */
  pendingCase?: RefObject<PendingCase | null>
  /** Carrier's minimum payout from the reference data (VerdictCard falls back to its default while it loads). */
  minimumPayout?: number
}

/**
 * Wynik: the verdict, then payout, save (signing in inline if needed) and the letter – a demand for money due, or a
 * complaint under the carrier's own rules when the EU 25%/50% scheme does not apply or is unclear.
 */
export function ResultStep({ state, dispatch, onOpenCases, pendingCase: sharedPending, minimumPayout }: ResultStepProps) {
  const { assessment, payout } = state
  const { user, loading, logout } = useAuth()
  const [rawPhase, setPhase] = useState<Phase>('idle')
  // Signed in again after an expired session: show the (re)preselected payout and wait for the user's own save.
  const phase: Phase = rawPhase === 'reauth' && user ? 'idle' : rawPhase
  const [error, setError] = useState<SaveError | null>(null)
  const [ibanError, setIbanError] = useState<string | null>(null)
  const [letter, setLetter] = useState<{ caseId: number; letter: ComplaintDto } | null>(null)
  const [profileIban, setProfileIban] = useState<string | null>(null)
  const ibanRef = useRef<HTMLInputElement>(null)
  const errorRef = useRef<HTMLDivElement>(null)
  const inFlight = useRef<AbortController | null>(null)
  const ownPending = useRef<PendingCase | null>(null)
  const pendingCase = sharedPending ?? ownPending
  const costs = toCaseRequest(state)?.expenseAmount ?? null
  const kind = letterKind(assessment, costs)
  const claimable = kind !== null
  const saveLabel = saveLabels[kind ?? 'compensation']
  const busy = phase === 'saving' || phase === 'writing'
  const sessionNoticeId = useId()
  const authOpen = (phase === 'auth' || phase === 'reauth') && !user && !loading
  const [sessionNotice, setSessionNotice] = useState(false)
  /** Set just before the 401 sign-out, so the account-change reset below knows why the account went away. */
  const sessionExpired = useRef(false)

  useEffect(() => () => inFlight.current?.abort(), [])

  // Signed out or another account: drop everything that belonged to the previous person (Wizard clears the state).
  useAccountChange(() => {
    inFlight.current?.abort()
    setLetter(null)
    setProfileIban(null)
    setError(null)
    setIbanError(null)
    // The payout was just cleared with the account: never resume a save the user has not confirmed again.
    if (sessionExpired.current) {
      sessionExpired.current = false
      setSessionNotice(true)
      setPhase('reauth')
    } else {
      setPhase('idle')
    }
  })

  // Signed in: preselect the payout and account number from the profile (unless the user already chose).
  const email = user?.email
  useEffect(() => {
    if (!email || !claimable) return
    let active = true
    api
      .getProfile()
      .then((profile) => {
        if (!active) return
        setProfileIban(profile.iban)
        dispatch({ type: 'profileLoaded', profile })
      })
      .catch(() => {})
    return () => {
      active = false
    }
  }, [email, claimable, dispatch])

  useEffect(() => {
    if (error) errorRef.current?.focus()
  }, [error])

  const ibanToSend = payout.choice === 'BankTransfer' && payout.iban.trim() !== '' ? normalizeIban(payout.iban) : null

  const checkIban = (): boolean => {
    if (ibanToSend !== null && !isValidIban(payout.iban)) {
      setIbanError('Sprawdź numer rachunku – wygląda na niepoprawny. Możesz go też pominąć.')
      return false
    }
    setIbanError(null)
    return true
  }

  /**
   * The id of this wizard's case, saved as `request`: joins a save still in flight, updates the saved case when the
   * ticket changed since (Back + edit), and creates one only when there is none. A create is never cancelled once
   * sent – it records `saved` even after the step unmounts – so nothing is duplicated or orphaned.
   */
  const ensureCase = async (request: CaseRequest, owner: string, signal: AbortSignal): Promise<number> => {
    const key = caseKey(request)
    let current: { id: number; key: string } | undefined
    const pending = pendingCase.current
    if (pending && pending.owner === owner) current = { id: await within(pending.promise, SAVE_TIMEOUT_MS), key: pending.key }
    else if (state.savedCase && state.savedCase.owner === owner) current = state.savedCase

    if (current && current.key === key) return current.id
    if (current) {
      try {
        await within(api.updateCase(current.id, request, signal), SAVE_TIMEOUT_MS)
        dispatch({ type: 'saved', caseId: current.id, key, owner })
        return current.id
      } catch (failure) {
        // Deleted meanwhile (e.g. from the cases list): save it anew.
        if (!(failure instanceof ApiError && failure.status === 404)) throw failure
      }
    }

    const entry: PendingCase = {
      key,
      owner,
      promise: api.createCase(request).then((created) => {
        if (pendingCase.current === entry) dispatch({ type: 'saved', caseId: created.id, key, owner })
        return created.id
      }),
    }
    pendingCase.current = entry
    entry.promise
      .finally(() => {
        if (pendingCase.current === entry) pendingCase.current = null
      })
      .catch(() => {})
    return within(entry.promise, SAVE_TIMEOUT_MS)
  }

  const save = async () => {
    setError(null)
    setSessionNotice(false)
    if (!checkIban()) {
      ibanRef.current?.focus()
      return
    }
    if (!user) {
      // The guest confirmed this payout: the profile loaded after signing in must not change it behind their back.
      dispatch({ type: 'payout', patch: { choice: payout.choice } })
      setPhase('auth')
      return
    }
    const request = toCaseRequest(state)
    if (!request) {
      setError({ kind: 'fix', step: 'ticket', messages: ['Brakuje danych biletu – uzupełnij przewoźnika, datę i cenę.'] })
      return
    }

    inFlight.current?.abort()
    const controller = new AbortController()
    inFlight.current = controller
    const owner = user.email
    let stage: 'save' | 'letter' = 'save'

    try {
      setPhase('saving')
      const caseId = await ensureCase(request, owner, controller.signal)

      stage = 'letter'
      setPhase('writing')
      const complaint: ComplaintRequest = {
        payout: payout.choice,
        oneTimeIban: ibanToSend,
        saveIbanToProfile: ibanToSend !== null && payout.remember,
      }
      const result = await within(api.generateComplaint(caseId, complaint, controller.signal), LETTER_TIMEOUT_MS)
      setLetter({ caseId, letter: result })
      setPhase('done')
    } catch (failure) {
      if (failure instanceof Timeout) controller.abort()
      else if (controller.signal.aborted || isAbort(failure)) return // left the step or signed out
      setPhase('idle')
      if (failure instanceof Timeout) {
        setError({
          kind: 'retry',
          message:
            stage === 'letter'
              ? 'Przygotowanie pisma trwa dłużej niż zwykle. Spróbuj ponownie – sprawa jest już zapisana.'
              : 'Zapisywanie trwa dłużej niż zwykle. Spróbuj ponownie – nie zapiszemy sprawy dwa razy.',
        })
        return
      }
      if (!(failure instanceof ApiError)) {
        setError({ kind: 'retry', message: 'Coś poszło nie tak. Spróbuj ponownie.' })
        return
      }
      if (failure.status === 401) {
        // The session expired: sign in again, then this continues on its own.
        sessionExpired.current = true
        await logout().catch(() => {})
        setSessionNotice(true)
        setPhase('reauth')
        return
      }
      if (failure.status === 409) {
        setError({ kind: 'final', message: failure.detail })
        return
      }
      if (failure.status === 400) {
        if (failure.fieldErrors.oneTimeIban) {
          setIbanError(failure.fieldErrors.oneTimeIban.join(' '))
          ibanRef.current?.focus()
          return
        }
        const keys = Object.keys(failure.fieldErrors)
        const messages = keys.length > 0 ? [...new Set(Object.values(failure.fieldErrors).flat())] : [failure.detail]
        setError({ kind: 'fix', messages, step: keys.length > 0 && keys.every((k) => delayFields.has(k)) ? 'delay' : 'ticket' })
        return
      }
      setError({ kind: 'retry', message: failure.detail })
    } finally {
      if (inFlight.current === controller) inFlight.current = null
    }
  }

  // After signing in (inline, or from the header) the save the user asked for carries on by itself.
  const saveRef = useRef(save)
  useEffect(() => {
    saveRef.current = save
  })
  useEffect(() => {
    if (phase === 'auth' && user) void saveRef.current()
  }, [phase, user])

  const ticket = toTicketFields(state.ticket)
  const route = caseRoute(ticket) ?? ''
  const details = tripDetails(ticket, route, state.delay, toCaseRequest(state))
  const done = phase === 'done' && letter !== null
  const reset = () => {
    pendingCase.current = null
    dispatch({ type: 'reset' })
  }

  return (
    <div className="wizard-step">
      <header className="wizard-step__head">
        <h2 className="wizard-step__title" tabIndex={-1}>
          Wynik
        </h2>
      </header>

      {assessment ? (
        <VerdictCard
          assessment={assessment}
          details={details}
          minimumPayout={minimumPayout}
          cancelled={state.delay.incidentType === 'Cancellation'}
        />
      ) : (
        <p className="wizard-step__lead">Brak wyniku – wróć i sprawdź dane.</p>
      )}

      {assessment?.outcome && <FinalVerdict outcome={assessment.outcome} tone={verdictStyle[shownVerdict(assessment)].tone} />}

      {assessment && kind !== 'compensation' && <NextSteps state={state} dispatch={dispatch} />}

      {claimable && !done && (
        <>
          {sessionNotice && (
            <div className="notice" role="status" aria-labelledby={sessionNoticeId}>
              <AlertCircleIcon className="notice__icon" size={20} />
              <div className="notice__body">
                <p className="notice__title" id={sessionNoticeId}>
                  {SESSION_EXPIRED}
                </p>
                <p className="notice__text">
                  {state.savedCase ? 'Sprawa jest zapisana – po zalogowaniu' : 'Po zalogowaniu'} wybierz formę wypłaty i naciśnij
                  „{saveLabel}”.
                </p>
              </div>
            </div>
          )}

          {kind === 'carrierRules' && (
            <p className="result__explain">
              Reklamacja poprosi przewoźnika o rozpatrzenie sprawy według jego regulaminu, bez gwarantowanej kwoty.
            </p>
          )}
          {kind === 'documentedCosts' && (
            <p className="result__explain">
              Wniosek poprosi przewoźnika o zwrot udokumentowanych kosztów – dołącz do niego rachunki lub paragony.
            </p>
          )}

          <PayoutChoice
            legend={
              kind === 'carrierRules'
                ? 'Jak chcesz otrzymać pieniądze, jeśli przewoźnik coś przyzna?'
                : kind === 'documentedCosts'
                  ? 'Jak chcesz otrzymać zwrot kosztów?'
                  : undefined
            }
            value={payout.choice}
            onChange={(choice) => dispatch({ type: 'payout', patch: { choice } })}
            iban={payout.iban}
            onIbanChange={(iban) => {
              setIbanError(null)
              dispatch({ type: 'payout', patch: { iban } })
            }}
            onIbanBlur={() => void checkIban()}
            ibanError={ibanError}
            ibanRef={ibanRef}
            remember={payout.remember}
            onRememberChange={(remember) => dispatch({ type: 'payout', patch: { remember } })}
            ibanSaved={profileIban !== null && ibanToSend !== null && normalizeIban(profileIban) === ibanToSend}
            disabled={busy}
          />

          {authOpen && (
            <AuthPanel
              key={phase}
              title={phase === 'reauth' ? 'Zaloguj się ponownie' : 'Zaloguj się, aby zapisać sprawę'}
              description={
                phase === 'reauth'
                  ? 'Ze względów bezpieczeństwa numer rachunku i forma wypłaty zostały wyczyszczone razem z sesją.'
                  : 'Nic z tego, co wpisałeś, nie przepadnie – po zalogowaniu od razu zapiszemy sprawę i przygotujemy pismo.'
              }
            />
          )}

          {busy && (
            <p className="result__progress" role="status">
              <span className="result__spinner" aria-hidden="true" />
              {phase === 'saving' ? 'Zapisuję sprawę…' : 'Przygotowuję pismo… to może potrwać kilkanaście sekund.'}
            </p>
          )}

          {error && (
            <div className="result-error" role="alert" tabIndex={-1} ref={errorRef} data-kind={error.kind}>
              <AlertCircleIcon className="result-error__icon" size={20} />
              <div className="result-error__body">
                {error.kind === 'fix' ? (
                  <>
                    <p className="result-error__title">Popraw dane, aby zapisać sprawę</p>
                    <ul className="result-error__list">
                      {error.messages.map((message) => (
                        <li key={message}>{message}</li>
                      ))}
                    </ul>
                  </>
                ) : (
                  <p className="result-error__title">{error.message}</p>
                )}
                {error.kind === 'fix' && (
                  <Button variant="secondary" size="sm" onClick={() => dispatch({ type: 'goto', step: error.step })}>
                    {error.step === 'ticket' ? 'Popraw dane biletu' : 'Popraw opóźnienie'}
                  </Button>
                )}
                {error.kind === 'retry' && (
                  <Button variant="secondary" size="sm" onClick={() => void save()}>
                    Spróbuj ponownie
                  </Button>
                )}
                {error.kind === 'final' && onOpenCases && state.savedCase && (
                  <Button variant="secondary" size="sm" onClick={onOpenCases}>
                    Przejdź do moich spraw
                  </Button>
                )}
              </div>
            </div>
          )}
        </>
      )}

      {done && <LetterChoice caseId={letter.caseId} letter={letter.letter} />}

      {done ? (
        <WizardActions>
          {() => (
            <>
              <Button variant="secondary" className={onOpenCases ? 'result__wrap' : 'wizard-actions__primary result__wrap'} onClick={reset}>
                Sprawdź kolejny bilet
              </Button>
              {onOpenCases && (
                <Button className="wizard-actions__primary result__wrap" iconEnd={<ArrowRightIcon size={18} />} onClick={onOpenCases}>
                  Przejdź do moich spraw
                </Button>
              )}
            </>
          )}
        </WizardActions>
      ) : claimable ? (
        <WizardActions
          onBack={busy ? undefined : () => dispatch({ type: 'back' })}
          hint={
            !authOpen
              ? undefined
              : phase === 'reauth'
                ? 'Zaloguj się ponownie, potem potwierdź formę wypłaty i zapisz.'
                : 'Zaloguj się lub załóż konto – potem zapiszemy sprawę automatycznie.'
          }
        >
          {(hintId) => (
            <Button
              className="wizard-actions__primary result__wrap"
              // While the sign-in panel is open, its button is the one primary action on screen.
              variant={authOpen ? 'secondary' : 'primary'}
              loading={busy}
              aria-describedby={hintId}
              onClick={() => void save()}
            >
              {saveLabel}
            </Button>
          )}
        </WizardActions>
      ) : (
        <WizardActions onBack={() => dispatch({ type: 'back' })}>
          {() => (
            <Button className="wizard-actions__primary" onClick={reset}>
              Sprawdź kolejny bilet
            </Button>
          )}
        </WizardActions>
      )}
    </div>
  )
}

/**
 * The trip as printed on the result's stub: only what the passenger actually entered.
 * Short facts first (they pair up in two columns), then the long ones that take a full row.
 */
function tripDetails(
  ticket: ReturnType<typeof toTicketFields>,
  route: string,
  delay: WizardState['delay'],
  request: CaseRequest | null,
): VerdictDetail[] {
  const rows: (VerdictDetail | null)[] = [
    ticket.carrier ? { label: 'Przewoźnik', value: [ticket.carrier, ticket.trainCategory].filter(Boolean).join(' · ') } : null,
    ticket.travelDate ? { label: 'Data przejazdu', value: formatDate(ticket.travelDate) } : null,
    ticket.ticketPrice !== null ? { label: 'Cena biletu', value: `${formatAmount(ticket.ticketPrice)} zł`, mono: true } : null,
    delay.incidentType === 'Cancellation'
      ? { label: 'Zdarzenie', value: 'Pociąg odwołany' }
      : delay.minutes !== null
        ? { label: 'Opóźnienie', value: `${delay.minutes} min`, mono: true }
        : null,
    route ? { label: 'Trasa', value: route, wide: true } : null,
    ticket.ticketNumber ? { label: 'Numer biletu', value: ticket.ticketNumber, mono: true, wide: true } : null,
    // Only what will be saved: a regional train with a usable amount.
    request?.expenseAmount != null ? { label: 'Poniesione koszty', value: `${formatAmount(request.expenseAmount)} zł`, mono: true } : null,
    request?.expenseDescription ? { label: 'Opis kosztów', value: request.expenseDescription, wide: true } : null,
  ]
  return rows.filter((row): row is VerdictDetail => row !== null)
}

/** No EU amount: say honestly why and what the passenger can still do. */
function NextSteps({ state, dispatch }: { state: WizardState; dispatch: Dispatch<WizardAction> }) {
  const titleId = useId()
  const assessment = state.assessment
  if (!assessment) return null
  const verdict = shownVerdict(assessment)
  const items: string[] = []
  let fix: Extract<WizardStep, 'ticket' | 'delay'> | null = null

  if (assessment.verdict === 'NotCovered') {
    items.push('Ten przewoźnik nie wypłaca odszkodowań wg przepisów UE – sprawdź jego regulamin.')
    items.push(
      state.expenses.amount.trim() !== ''
        ? 'Przygotujemy poniżej wniosek o zwrot poniesionych kosztów. Wyślesz go przewoźnikowi e-mailem, przez jego formularz albo w kasie – z kopiami rachunków.'
        : 'Możesz złożyć reklamację – przygotujemy ją poniżej. Wyślesz ją przewoźnikowi e-mailem, przez jego formularz albo w kasie.',
    )
    items.push('Jeśli przewoźnik lub kategoria pociągu są inne niż wpisane, popraw dane biletu.')
    fix = 'ticket'
  } else if (state.delay.isSeasonTicket) {
    items.push('Bilety okresowe rozlicza się indywidualnie – złóż reklamację bezpośrednio u przewoźnika i podaj numer biletu.')
    items.push('Przy częstych opóźnieniach na trasie przewoźnik może przyznać rekompensatę za cały okres ważności biletu.')
  } else if (assessment.priceRequired) {
    items.push('Wpisz cenę z biletu – od niej zależy kwota odszkodowania.')
    fix = 'ticket'
  } else if (verdict === 'NotCovered') {
    items.push('Odszkodowanie przysługuje od 60 minut opóźnienia na stacji docelowej.')
    items.push('Jeśli pociąg przyjechał później, niż wpisałeś, popraw opóźnienie.')
    items.push('Możesz też złożyć reklamację – przygotujemy ją poniżej, a przewoźnik rozpatrzy ją według swojego regulaminu.')
    fix = 'delay'
  } else {
    items.push('Nie rozpoznaliśmy przewoźnika lub kategorii pociągu – od nich zależy, czy należy się odszkodowanie.')
    items.push('Sprawdź je na bilecie i popraw dane. W razie wątpliwości zapytaj przewoźnika.')
    fix = 'ticket'
  }

  return (
    <section className="next-steps" aria-labelledby={titleId}>
      <h3 className="next-steps__title" id={titleId}>
        Co możesz zrobić
      </h3>
      <ul className="next-steps__list">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
      {fix && (
        <Button variant="secondary" size="sm" className="next-steps__fix" onClick={() => dispatch({ type: 'goto', step: fix })}>
          {fix === 'ticket' ? 'Popraw dane biletu' : 'Popraw opóźnienie'}
        </Button>
      )}
    </section>
  )
}
