import { useId } from 'react'
import { Link } from 'react-router'
import type { CaseDto, CaseStatus } from '../api/types'
import { Badge } from '../components/Badge'
import { AlertCircleIcon, ChevronRightIcon, TrashIcon } from '../components/icons'
import { formatDate } from '../lib/format'
import { verdictStyle } from '../lib/verdict'
import { caseRoute, caseTitle, caseVerdict, formatPln } from './caseText'
import { DeadlineBadge } from './DeadlineBadge'
import { StatusSelect } from './StatusSelect'

interface CaseRowProps {
  item: CaseDto
  error?: string
  onStatusChange: (status: CaseStatus) => void
  onDelete: () => void
}

/** One case as a row: what and when on the left, amount and verdict, then status and delete. */
export function CaseRow({ item, error, onStatusChange, onDelete }: CaseRowProps) {
  const id = useId()
  const route = caseRoute(item)
  const verdict = verdictStyle[caseVerdict(item)]
  const title = caseTitle(item)

  return (
    <li className="case-row">
      <div className="case-row__main">
        <Link to={`/sprawy/${item.id}`} className="case-row__title" id={`${id}-title`}>
          {title}
          <ChevronRightIcon size={18} />
        </Link>
        <p className="case-row__meta">
          {route ? <span>{route}</span> : <span className="case-row__unknown">trasa nieznana</span>}
          <span aria-hidden="true"> · </span>
          <span>
            <span className="visually-hidden">przejazd </span>
            {formatDate(item.travelDate)}
          </span>
        </p>
        <DeadlineBadge status={item.status} responseDueDate={item.responseDueDate} />
      </div>

      <div className="case-row__money">
        <span className="case-row__amount mono tabular">{formatPln(item.compensationAmount)}</span>
        <Badge tone={verdict.tone}>{verdict.label}</Badge>
      </div>

      <div className="case-row__controls">
        <StatusSelect value={item.status} onChange={onStatusChange} caseLabelId={`${id}-title`} />
        <button type="button" className="case-row__delete" onClick={onDelete} aria-label={`Usuń sprawę: ${title}, ${formatDate(item.travelDate)}`}>
          <TrashIcon size={20} />
        </button>
      </div>

      {error && (
        <p className="case-row__error" role="alert">
          <AlertCircleIcon size={16} />
          {error}
        </p>
      )}
    </li>
  )
}
