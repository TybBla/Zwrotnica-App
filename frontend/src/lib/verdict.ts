import type { AssessmentDto, Verdict } from '../api/types'
import type { BadgeTone } from '../components/Badge'

/** Verdict colors follow rail signals (green / muted red / amber): one place that maps a verdict to its tone and label. */
export const verdictStyle: Record<Verdict, { tone: BadgeTone; label: string }> = {
  Eligible: { tone: 'success', label: 'Należy się' },
  NotCovered: { tone: 'danger', label: 'Nie należy się' },
  Unknown: { tone: 'warning', label: 'Do sprawdzenia' },
}

/**
 * The verdict as the passenger should read it. The API says "Eligible" for a covered train even when nothing is due
 * (delay under 60 min) or the amount cannot be computed (no price) – those read as "Nie należy się" / "Do sprawdzenia".
 */
export function shownVerdict(assessment: Pick<AssessmentDto, 'verdict' | 'compensationAmount' | 'priceRequired'>): Verdict {
  if (assessment.verdict !== 'Eligible') return assessment.verdict
  if (assessment.priceRequired) return 'Unknown'
  return assessment.compensationAmount > 0 ? 'Eligible' : 'NotCovered'
}

/**
 * Which letter a case gets, mirroring ComplaintService: money due → a demand under the EU rules ('compensation');
 * a train outside the EU 25%/50% scheme or an unclear one → a complaint the carrier decides under its own rules
 * ('carrierRules'); a covered train with nothing due (under 60 min, no price yet) → none (the API answers 409).
 */
export type LetterKind = 'compensation' | 'carrierRules'

export function letterKind(
  assessment: Pick<AssessmentDto, 'verdict' | 'compensationAmount' | 'priceRequired'> | undefined,
): LetterKind | null {
  if (!assessment) return null
  if (assessment.verdict === 'NotCovered' || assessment.verdict === 'Unknown') return 'carrierRules'
  return shownVerdict(assessment) === 'Eligible' ? 'compensation' : null
}
