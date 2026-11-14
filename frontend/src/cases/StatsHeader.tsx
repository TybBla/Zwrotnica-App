import type { StatsDto } from '../api/types'
import { casesCount, formatPln } from './caseText'

/** "Odzyskano X zł · Czeka Y zł · N spraw" – paid cases, money still open (new + sent), and the count. */
export function StatsHeader({ stats }: { stats: StatsDto | null }) {
  if (!stats) return <p className="cases-stats cases-stats--pending" aria-hidden="true" />
  return (
    <p className="cases-stats" data-summary>
      <span className="cases-stats__item">
        Odzyskano <strong className="cases-stats__value mono">{formatPln(stats.recovered)}</strong>
      </span>
      <span className="cases-stats__sep" aria-hidden="true">
        {' · '}
      </span>
      <span className="cases-stats__item">
        Czeka <strong className="cases-stats__value mono">{formatPln(stats.pending)}</strong>
      </span>
      <span className="cases-stats__sep" aria-hidden="true">
        {' · '}
      </span>
      <span className="cases-stats__item">{casesCount(stats.totalCases)}</span>
    </p>
  )
}
