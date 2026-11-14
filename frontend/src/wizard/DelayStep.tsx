import { useEffect, useRef, useState, type Dispatch } from 'react'
import { api } from '../api/client'
import type { AssessmentDto, AssessmentRequest, DelayCause } from '../api/types'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { Checkbox } from '../components/Checkbox'
import { Field } from '../components/Field'
import { AlertCircleIcon, ArrowRightIcon } from '../components/icons'
import { Select, type SelectOption } from '../components/Select'
import { TextInput } from '../components/TextInput'
import { toAssessmentRequest, type WizardAction, type WizardState } from '../hooks/useWizard'
import { formatAmount } from '../lib/format'
import { verdictStyle } from '../lib/verdict'
import { WizardActions } from './WizardActions'

const QUICK_MINUTES = [60, 90, 120, 180] as const
const ASSESS_DEBOUNCE_MS = 300

const NO_CAUSE = ''

const causeOptions: SelectOption<DelayCause | typeof NO_CAUSE>[] = [
  { value: NO_CAUSE, label: '— nie wiem —' },
  { value: 'Unknown', label: 'Nieznana' },
  { value: 'Weather', label: 'Pogoda' },
  { value: 'PersonsOnTrack', label: 'Osoby na torach' },
  { value: 'RailStrike', label: 'Strajk personelu kolejowego' },
  { value: 'Technical', label: 'Awaria techniczna' },
  { value: 'Other', label: 'Inna' },
]

const CAUSE_TOOLTIP =
  'Warto podać przyczynę: siła wyższa (np. ekstremalna pogoda, osoby na torach) może zwolnić przewoźnika z wypłaty. Strajk personelu kolejowego nie zwalnia.'

interface DelayStepProps {
  state: WizardState
  dispatch: Dispatch<WizardAction>
}

/**
 * Assesses the current request ~300 ms after the last change, so the preview follows typing without a request per
 * keystroke. Answers to an older request are dropped, and nothing is reported after the step unmounts (requests are
 * aborted). `assessNow` skips the wait (used by "Zobacz wynik").
 */
function useLiveAssessment(request: AssessmentRequest | null, onResult: (assessment: AssessmentDto) => void) {
  const key = request ? JSON.stringify(request) : null
  /** The last request that got an answer, and the last one that failed (state, so the preview re-renders). */
  const [okKey, setOkKey] = useState<string | null>(null)
  const [failedKey, setFailedKey] = useState<string | null>(null)
  const latestKey = useRef(key)
  const assessedKey = useRef<string | null>(null)
  const timer = useRef<number | undefined>(undefined)
  const inFlight = useRef<AbortController | null>(null)
  const mounted = useRef(false)
  const onResultRef = useRef(onResult)
  useEffect(() => {
    onResultRef.current = onResult
    latestKey.current = key
  })

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      inFlight.current?.abort()
    }
  }, [])

  // Only the newest request matters: starting one aborts the previous; a late or stale answer is dropped.
  const run = async (requestKey: string) => {
    inFlight.current?.abort()
    const controller = new AbortController()
    inFlight.current = controller
    try {
      const assessment = await api.assess(JSON.parse(requestKey) as AssessmentRequest, controller.signal)
      if (controller.signal.aborted || !mounted.current) throw new DOMException('Superseded', 'AbortError')
      if (requestKey !== latestKey.current) return
      assessedKey.current = requestKey
      setOkKey(requestKey)
      onResultRef.current(assessment)
    } catch (error) {
      if (!controller.signal.aborted && mounted.current && requestKey === latestKey.current) setFailedKey(requestKey)
      throw error
    } finally {
      if (inFlight.current === controller) inFlight.current = null
    }
  }

  useEffect(() => {
    if (key === null || key === assessedKey.current) return
    const handle = window.setTimeout(() => {
      run(key).catch(() => {})
    }, ASSESS_DEBOUNCE_MS)
    timer.current = handle
    return () => window.clearTimeout(handle)
  }, [key])

  /** Resolves once the current request has an assessment; rejects when the call failed or was superseded. */
  const assessNow = async () => {
    if (key === null || key === assessedKey.current) return
    window.clearTimeout(timer.current)
    await run(key)
  }

  return {
    pending: key !== null && key !== okKey && key !== failedKey,
    failed: key !== null && key === failedKey && key !== okKey,
    assessNow,
  }
}

export function DelayStep({ state, dispatch }: DelayStepProps) {
  const { delay, assessment } = state
  const cancelled = delay.incidentType === 'Cancellation'
  const request = toAssessmentRequest(state)
  const live = useLiveAssessment(request, (result) => dispatch({ type: 'assessed', assessment: result }))
  const [moving, setMoving] = useState(false)
  const [moveFailed, setMoveFailed] = useState(false)

  const setMinutes = (text: string) => {
    const digits = text.replace(/\D/g, '').slice(0, 4)
    dispatch({ type: 'delay', patch: { minutes: digits === '' ? null : Number(digits) } })
  }

  const showResult = async () => {
    setMoving(true)
    setMoveFailed(false)
    try {
      await live.assessNow()
      dispatch({ type: 'next' })
    } catch {
      setMoveFailed(true)
      setMoving(false)
    }
  }

  const minutesMissing = !cancelled && delay.minutes === null

  return (
    <div className="wizard-step">
      <header className="wizard-step__head">
        <h2 className="wizard-step__title" tabIndex={-1}>
          Opóźnienie
        </h2>
        <p className="wizard-step__lead">Liczy się opóźnienie na stacji docelowej – tyle, ile pociąg przyjechał później niż w rozkładzie.</p>
      </header>

      <div className="delay-form">
        <Checkbox
          variant="switch"
          label="Pociąg odwołany"
          description="Przy odwołaniu należy się zwrot pełnej ceny biletu."
          checked={cancelled}
          onCheckedChange={(checked) => dispatch({ type: 'delay', patch: { incidentType: checked ? 'Cancellation' : 'Delay' } })}
        />

        {!cancelled && (
          <div className="wizard-swap stack">
            <Field label="Opóźnienie na stacji docelowej" required hint="60–119 min to 25% ceny biletu, od 120 min – 50%.">
              <TextInput
                value={delay.minutes === null ? '' : String(delay.minutes)}
                onValueChange={setMinutes}
                inputMode="numeric"
                pattern="[0-9]*"
                enterKeyHint="done"
                autoComplete="off"
                placeholder="np. 75"
                suffix="min"
                className="tabular delay-form__minutes"
              />
            </Field>
            <div className="chips" role="group" aria-label="Szybki wybór opóźnienia">
              {QUICK_MINUTES.map((minutes) => (
                <button
                  key={minutes}
                  type="button"
                  className="chip tabular"
                  aria-pressed={delay.minutes === minutes}
                  onClick={() => dispatch({ type: 'delay', patch: { minutes } })}
                >
                  {minutes} min
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Right under the minutes, so the amount is on screen while the user picks them (also on phones). */}
        <LivePreview
          assessment={request ? assessment : undefined}
          pending={live.pending}
          failed={live.failed}
          waitingFor={minutesMissing ? 'minutes' : request === null ? 'price' : null}
        />

        <Field label="Przyczyna opóźnienia" optional tooltip={CAUSE_TOOLTIP}>
          <Select
            value={delay.cause ?? NO_CAUSE}
            onValueChange={(cause) => dispatch({ type: 'delay', patch: { cause: cause === NO_CAUSE ? undefined : cause } })}
            options={causeOptions}
          />
        </Field>

        <Checkbox
          label="Bilet okresowy"
          description="Np. miesięczny – kwotę wylicza się wtedy indywidualnie."
          checked={delay.isSeasonTicket}
          onCheckedChange={(isSeasonTicket) => dispatch({ type: 'delay', patch: { isSeasonTicket } })}
        />
      </div>

      <WizardActions
        onBack={() => dispatch({ type: 'back' })}
        hint={
          moveFailed
            ? 'Nie udało się teraz sprawdzić wyniku. Spróbuj ponownie.'
            : minutesMissing
              ? 'Podaj opóźnienie w minutach albo zaznacz, że pociąg odwołano.'
              : undefined
        }
      >
        {(hintId) => (
          <Button
            className="wizard-actions__primary"
            iconEnd={<ArrowRightIcon size={18} />}
            disabled={request === null}
            loading={moving}
            aria-describedby={hintId}
            onClick={showResult}
          >
            Zobacz wynik
          </Button>
        )}
      </WizardActions>
    </div>
  )
}

interface LivePreviewProps {
  assessment: AssessmentDto | undefined
  pending: boolean
  failed: boolean
  waitingFor: 'minutes' | 'price' | null
}

/** A compact, always-visible answer to "how much?", updated as the user types. */
function LivePreview({ assessment, pending, failed, waitingFor }: LivePreviewProps) {
  const style = assessment ? verdictStyle[assessment.verdict] : null
  return (
    <section className="live" aria-label="Wstępny wynik" data-pending={pending || undefined}>
      <div aria-live="polite" className="live__content">
        {waitingFor === 'minutes' ? (
          <p className="live__placeholder">Podaj opóźnienie – od razu pokażemy, ile może Ci się należeć.</p>
        ) : waitingFor === 'price' ? (
          <p className="live__placeholder">Brakuje poprawnej ceny biletu – wróć do poprzedniego kroku.</p>
        ) : failed ? (
          // The amount on screen would belong to older answers – say plainly that this one could not be computed.
          <p className="live__placeholder">Nie udało się teraz policzyć kwoty – sprawdzimy ją po przejściu dalej.</p>
        ) : assessment && style ? (
          <>
            <div className="live__head">
              <Badge tone={style.tone}>{style.label}</Badge>
              {assessment.verdict === 'Eligible' && assessment.compensationAmount > 0 && (
                <p className="live__amount tabular">
                  {formatAmount(assessment.compensationAmount)}
                  <span className="live__currency"> zł</span>
                </p>
              )}
            </div>
            <p className="live__reason">{assessment.reason}</p>
            {assessment.refusalRisk && (
              <p className="live__risk">
                <AlertCircleIcon size={16} />
                Przy tej przyczynie przewoźnik może odmówić wypłaty (siła wyższa).
              </p>
            )}
          </>
        ) : (
          <p className="live__placeholder">Liczę…</p>
        )}
      </div>
    </section>
  )
}
