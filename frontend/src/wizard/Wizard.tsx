import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { ReferenceDataDto } from '../api/types'
import { useWizard, type WizardStep } from '../hooks/useWizard'
import { DelayStep } from './DelayStep'
import { ResultPlaceholder } from './ResultPlaceholder'
import { StartChoice } from './StartChoice'
import { StepIndicator } from './StepIndicator'
import { TicketStep } from './TicketStep'
import './Wizard.css'

const indicatorIndex: Record<WizardStep, number> = { start: 0, ticket: 0, delay: 1, result: 2 }

/** Bilet → Opóźnienie → Wynik. Works for guests; nothing entered is lost when moving back or when a read fails. */
export function Wizard() {
  const { state, dispatch } = useWizard()
  const [reference, setReference] = useState<ReferenceDataDto | null>(null)
  const rootRef = useRef<HTMLElement>(null)
  const firstRender = useRef(true)

  useEffect(() => {
    let active = true
    // Without reference data the form still works: the carrier is then typed in ("Inny").
    api
      .getReference()
      .then((data) => active && setReference(data))
      .catch(() => {})
    return () => {
      active = false
    }
  }, [])

  // A new step starts at its heading: scroll it into view and move focus there for screen readers and keyboards.
  useLayoutEffect(() => {
    if (firstRender.current) {
      firstRender.current = false
      return
    }
    const root = rootRef.current
    if (!root) return
    if (root.getBoundingClientRect().top < 0) root.scrollIntoView({ block: 'start' })
    root.querySelector<HTMLElement>('.wizard-step__title')?.focus({ preventScroll: true })
  }, [state.step])

  return (
    <section ref={rootRef} className="wizard container" aria-label="Kreator reklamacji">
      <StepIndicator current={indicatorIndex[state.step]} />
      <div className="wizard__body" key={state.step} data-direction={state.direction}>
        {state.step === 'start' && <StartChoice onChoose={(source) => dispatch({ type: 'start', source })} />}
        {state.step === 'ticket' && <TicketStep state={state} dispatch={dispatch} reference={reference} />}
        {state.step === 'delay' && <DelayStep state={state} dispatch={dispatch} />}
        {state.step === 'result' && <ResultPlaceholder state={state} dispatch={dispatch} />}
      </div>
    </section>
  )
}
