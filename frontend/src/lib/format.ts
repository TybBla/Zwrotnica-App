const pln = new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** 42.5 → "42,50" (amount only; add " zł" where it is shown). */
export function formatAmount(value: number): string {
  return pln.format(value)
}
