import { useState } from 'react'
import type { CaseStatus, IsoDateTime } from '../api/types'
import { ClockIcon, ExternalLinkIcon, WarningIcon } from '../components/icons'
import { daysBetween, daysLabel, warsawDate } from '../lib/deadline'
import { formatDate } from '../lib/format'
import './Cases.css'

/** UTK (rail regulator) – the site root: its complaint pages move, so no deep link is guessed here. */
export const UTK_URL = 'https://utk.gov.pl/'

interface DeadlineBadgeProps {
  status: CaseStatus
  responseDueDate: IsoDateTime | null
  /** For tests; defaults to the current time. */
  now?: Date
}

/** Only a sent complaint has a clock: the days left for the carrier's answer, then where to escalate. */
export function DeadlineBadge({ status, responseDueDate, now }: DeadlineBadgeProps) {
  // Read once per mount: the count is in whole days, a re-render must not shift it.
  const [mountedAt] = useState(() => new Date())
  if (status !== 'Sent' || !responseDueDate) return null

  const due = warsawDate(new Date(responseDueDate))
  const left = daysBetween(warsawDate(now ?? mountedAt), due)

  if (left < 0) {
    return (
      <p className="deadline deadline--overdue">
        <WarningIcon size={16} />
        <span>
          Brak odpowiedzi? Możesz złożyć skargę do UTK{' '}
          <a href={UTK_URL} target="_blank" rel="noreferrer" className="deadline__link">
            utk.gov.pl
            <ExternalLinkIcon size={14} />
            <span className="visually-hidden"> (UTK, otwiera się w nowej karcie)</span>
          </a>
        </span>
      </p>
    )
  }

  return (
    <p className="deadline">
      <ClockIcon size={16} />
      <span>
        Odpowiedź do {formatDate(due)} · {left === 0 ? 'termin mija dziś' : `jeszcze ${daysLabel(left)}`}
      </span>
    </p>
  )
}
