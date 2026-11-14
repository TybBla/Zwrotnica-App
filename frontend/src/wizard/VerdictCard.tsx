import { useId } from 'react'
import type { AssessmentDto } from '../api/types'
import { AlertCircleIcon, CheckCircleIcon, InfoIcon } from '../components/icons'
import { formatAmount } from '../lib/format'
import { shownVerdict, verdictStyle } from '../lib/verdict'

/** Carrier's minimum payout (backend CompensationOptions.MinimumPayout). */
export const MINIMUM_PAYOUT = 16

const verdictIcon = {
  Eligible: CheckCircleIcon,
  NotCovered: InfoIcon,
  Unknown: AlertCircleIcon,
} as const

/** The answer: verdict, amount, why – and anything that may still stand in the way of the payout. */
export function VerdictCard({ assessment }: { assessment: AssessmentDto }) {
  const id = useId()
  const verdict = shownVerdict(assessment)
  const style = verdictStyle[verdict]
  const Icon = verdictIcon[verdict]
  const showAmount = verdict === 'Eligible'
  // The server's reason already mentions the threshold; the notice below says it in full, so keep the reason short.
  const reason = assessment.belowMinimum ? `${assessment.percent}% ceny zapłaconego biletu.` : assessment.reason

  return (
    <section className="verdict" data-tone={style.tone} aria-labelledby={`${id}-title`}>
      <div className="verdict__head">
        <Icon className="verdict__icon" size={22} />
        <h3 className="verdict__title" id={`${id}-title`}>
          {style.label}
        </h3>
      </div>

      {showAmount && (
        <p className="verdict__amount tabular">
          {formatAmount(assessment.compensationAmount)}
          <span className="verdict__currency"> zł</span>
        </p>
      )}

      <p className="verdict__reason">{reason}</p>

      {(assessment.belowMinimum || assessment.priceRequired || assessment.refusalRisk) && (
        <ul className="verdict__notes">
          {assessment.belowMinimum && (
            <li className="verdict__note">
              <AlertCircleIcon size={18} />
              <span>
                Poniżej progu – kwota jest niższa niż minimalna wypłata przewoźnika ({MINIMUM_PAYOUT} zł), więc przewoźnik może
                jej nie wypłacić. Reklamację możesz złożyć mimo to.
              </span>
            </li>
          )}
          {assessment.priceRequired && (
            <li className="verdict__note">
              <AlertCircleIcon size={18} />
              <span>Podaj cenę zapłaconego biletu – bez niej nie obliczymy kwoty.</span>
            </li>
          )}
          {assessment.refusalRisk && (
            <li className="verdict__note" role="note">
              <AlertCircleIcon size={18} />
              <span>
                Przewoźnik może odmówić wypłaty: przy tej przyczynie (np. ekstremalna pogoda, osoby na torach) siła wyższa może
                zwolnić go z odpowiedzialności. Strajk personelu kolejowego nie zwalnia.
              </span>
            </li>
          )}
        </ul>
      )}
    </section>
  )
}
