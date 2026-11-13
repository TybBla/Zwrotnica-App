import type { Verdict } from '../api/types'
import type { BadgeTone } from '../components/Badge'

/** The semantic palette for verdicts: one place that maps a verdict to its tone and Polish label. */
export const verdictStyle: Record<Verdict, { tone: BadgeTone; label: string }> = {
  Eligible: { tone: 'success', label: 'Należy się' },
  NotCovered: { tone: 'neutral', label: 'Nie należy się' },
  Unknown: { tone: 'warning', label: 'Do sprawdzenia' },
}
