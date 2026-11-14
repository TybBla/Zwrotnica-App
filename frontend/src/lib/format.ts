const pln = new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** 42.5 → "42,50" (amount only; add " zł" where it is shown). */
export function formatAmount(value: number): string {
  return pln.format(value)
}

const longDate = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'long', year: 'numeric' })

/** "2025-06-01" → "1 czerwca 2025" (a calendar date, read in local time; anything else is shown as given). */
export function formatDate(iso: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  if (!match) return iso
  return longDate.format(new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])))
}
