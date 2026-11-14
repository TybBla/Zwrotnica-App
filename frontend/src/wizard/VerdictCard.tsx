import { useId } from 'react'
import type { AssessmentDto } from '../api/types'
import { AlertCircleIcon, CheckCircleIcon, MinusCircleIcon, WarningIcon } from '../components/icons'
import { formatAmount } from '../lib/format'
import { shownVerdict, verdictStyle } from '../lib/verdict'

/** Carrier's minimum payout (backend CompensationOptions.MinimumPayout). */
export const MINIMUM_PAYOUT = 16

const verdictIcon = {
  Eligible: CheckCircleIcon,
  NotCovered: MinusCircleIcon,
  Unknown: AlertCircleIcon,
} as const

/** One line of the trip data printed on the result (the ticket-stub part). */
export interface VerdictDetail {
  label: string
  value: string
  /** Ticket data such as the ticket number, set in Plex Mono. */
  mono?: boolean
  /** Long values (route, ticket number) take a whole row. */
  wide?: boolean
}

interface VerdictCardProps {
  assessment: AssessmentDto
  /** The trip the verdict is about, shown under a perforation like the stub of a ticket. */
  details?: VerdictDetail[]
}

/** The answer, laid out like a receipt: verdict, amount, why, what may stand in the way – and the trip it is about. */
export function VerdictCard({ assessment, details = [] }: VerdictCardProps) {
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

      <div className="verdict__body">
        {showAmount && (
          <div className="verdict__amount-block">
            <p className="verdict__amount-label">Kwota odszkodowania</p>
            <p className="verdict__amount tabular">
              {formatAmount(assessment.compensationAmount)}
              <span className="verdict__currency"> zł</span>
            </p>
          </div>
        )}

        <p className="verdict__reason">{reason}</p>

        {(assessment.belowMinimum || assessment.priceRequired || assessment.refusalRisk) && (
          <ul className="verdict__notes">
            {assessment.belowMinimum && (
              <li className="verdict__note">
                <WarningIcon size={18} />
                <span>
                  Poniżej progu – kwota jest niższa niż minimalna wypłata przewoźnika ({MINIMUM_PAYOUT} zł), więc przewoźnik może
                  jej nie wypłacić. Reklamację możesz złożyć mimo to.
                </span>
              </li>
            )}
            {assessment.priceRequired && (
              <li className="verdict__note">
                <WarningIcon size={18} />
                <span>Podaj cenę zapłaconego biletu – bez niej nie obliczymy kwoty.</span>
              </li>
            )}
            {assessment.refusalRisk && (
              <li className="verdict__note" role="note">
                <WarningIcon size={18} />
                <span>
                  Przewoźnik może odmówić wypłaty: przy tej przyczynie (np. ekstremalna pogoda, osoby na torach) siła wyższa może
                  zwolnić go z odpowiedzialności. Strajk personelu kolejowego nie zwalnia.
                </span>
              </li>
            )}
          </ul>
        )}
      </div>

      {details.length > 0 && (
        <>
          <div className="verdict__perforation" aria-hidden="true" />
          <dl className="verdict__details">
            {details.map((detail) => (
              <div key={detail.label} className="verdict__detail" data-wide={detail.wide || undefined}>
                <dt>{detail.label}</dt>
                <dd className={detail.mono ? 'mono' : undefined}>{detail.value}</dd>
              </div>
            ))}
          </dl>
        </>
      )}
    </section>
  )
}
