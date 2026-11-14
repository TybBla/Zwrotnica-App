import type { CaseDto, DelayCause, PayoutPreference } from '../api/types'
import { formatAmount } from '../lib/format'
import { letterKind, shownVerdict } from '../lib/verdict'

/** "PKP Intercity IC" */
export const caseTitle = (item: Pick<CaseDto, 'carrier' | 'trainCategory'>) =>
  [item.carrier, item.trainCategory].filter(Boolean).join(' ')

/**
 * "Kraków Główny → Warszawa Centralna"; with one station only "Z: Kraków Główny" / "Do: Warszawa Centralna"
 * (no orphaned "?"); null when neither is known.
 */
export function caseRoute(item: Pick<CaseDto, 'fromStation' | 'toStation'>): string | null {
  if (item.fromStation && item.toStation) return `${item.fromStation} → ${item.toStation}`
  if (item.fromStation) return `Z: ${item.fromStation}`
  if (item.toStation) return `Do: ${item.toStation}`
  return null
}

/** A stored case has a price, so only the 0 zł "Eligible" (under 60 min) needs re-reading as not due. */
export const caseVerdict = (item: Pick<CaseDto, 'verdict' | 'compensationAmount'>) =>
  shownVerdict({ verdict: item.verdict, compensationAmount: item.compensationAmount, priceRequired: false })

/** The letter a saved case gets (a saved case always has a price). */
export const caseLetterKind = (item: Pick<CaseDto, 'verdict' | 'compensationAmount' | 'expenseAmount'>) =>
  letterKind({ verdict: item.verdict, compensationAmount: item.compensationAmount, priceRequired: false }, item.expenseAmount)

export const formatPln = (value: number) => `${formatAmount(value)} zł`

/** Polish count of cases: 1 sprawa, 2–4 sprawy (but 12–14 spraw), 5+ spraw. */
export function casesCount(count: number): string {
  const lastDigit = count % 10
  const lastTwo = count % 100
  const word =
    count === 1 ? 'sprawa' : lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? 'sprawy' : 'spraw'
  return `${count} ${word}`
}

export const causeLabels: Record<DelayCause, string> = {
  Unknown: 'Nieznana',
  Weather: 'Pogoda',
  PersonsOnTrack: 'Osoby na torach',
  RailStrike: 'Strajk personelu kolejowego',
  Technical: 'Awaria techniczna',
  Other: 'Inna',
}

export const payoutLabels: Record<PayoutPreference, string> = {
  OriginalPayment: 'Zwrot na konto/kartę użyte do zakupu',
  BankTransfer: 'Przelew na inny rachunek',
  Voucher: 'Bon',
}

/** "Opóźnienie 120 min" or "Odwołanie pociągu". */
export const incidentText = (item: Pick<CaseDto, 'incidentType' | 'delayMinutes'>) =>
  item.incidentType === 'Cancellation' ? 'Odwołanie pociągu' : `Opóźnienie ${item.delayMinutes} min`
