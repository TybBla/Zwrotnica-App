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
 * a regional train with an amount of costs entered → a claim for those documented costs ('documentedCosts');
 * anything else – a train outside the EU 25%/50% scheme, an unclear one, or a covered train with nothing due (e.g.
 * 45 min late) → a complaint the carrier decides under its own rules ('carrierRules'). Filing is never blocked;
 * only a missing price means no letter yet (the API answers 409).
 */
export type LetterKind = 'compensation' | 'carrierRules' | 'documentedCosts'

export function letterKind(
  assessment: Pick<AssessmentDto, 'verdict' | 'compensationAmount' | 'priceRequired'> | undefined,
  expenseAmount?: number | null,
): LetterKind | null {
  if (!assessment) return null
  if (assessment.verdict === 'Eligible' && assessment.priceRequired) return null
  if (assessment.verdict === 'NotCovered' && expenseAmount != null && expenseAmount > 0) return 'documentedCosts'
  return shownVerdict(assessment) === 'Eligible' ? 'compensation' : 'carrierRules'
}
