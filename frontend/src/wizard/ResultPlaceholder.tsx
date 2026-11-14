import type { Dispatch } from 'react'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { AlertCircleIcon } from '../components/icons'
import { toTicketFields, type WizardAction, type WizardState } from '../hooks/useWizard'
import { formatAmount } from '../lib/format'
import { verdictStyle } from '../lib/verdict'
import { WizardActions } from './WizardActions'

/** Temporary "Wynik" body (Task 10 replaces it with the verdict card, payout, save and letter). */
export function ResultPlaceholder({ state, dispatch }: { state: WizardState; dispatch: Dispatch<WizardAction> }) {
  const { assessment } = state
  const ticket = toTicketFields(state.ticket)
  const route = [ticket.fromStation, ticket.toStation].filter(Boolean).join(' → ')
  const style = assessment ? verdictStyle[assessment.verdict] : null

  return (
    <div className="wizard-step">
      <header className="wizard-step__head">
        <h2 className="wizard-step__title" tabIndex={-1}>
          Wynik
        </h2>
        <p className="wizard-step__lead">{[ticket.carrier, ticket.trainCategory, route].filter(Boolean).join(' · ')}</p>
      </header>

      <Card elevation="raised" padding="lg" className="result-placeholder">
        {assessment && style ? (
          <>
            <Badge tone={style.tone}>{style.label}</Badge>
            {assessment.compensationAmount > 0 && (
              <p className="result-placeholder__amount tabular">
                {formatAmount(assessment.compensationAmount)}
                <span className="result-placeholder__currency"> zł</span>
              </p>
            )}
            <p className="result-placeholder__reason">{assessment.reason}</p>
            {assessment.refusalRisk && (
              <p className="live__risk">
                <AlertCircleIcon size={16} />
                Przy tej przyczynie przewoźnik może odmówić wypłaty (siła wyższa).
              </p>
            )}
          </>
        ) : (
          <p className="result-placeholder__reason">Brak wyniku – wróć i sprawdź dane.</p>
        )}
      </Card>

      <WizardActions onBack={() => dispatch({ type: 'back' })}>
        {() => (
          <Button className="wizard-actions__primary" variant="secondary" onClick={() => dispatch({ type: 'reset' })}>
            Sprawdź inny bilet
          </Button>
        )}
      </WizardActions>
    </div>
  )
}
