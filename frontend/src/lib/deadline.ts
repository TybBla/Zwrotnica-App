// Deadlines are calendar days as a passenger in Poland reads them: dates in Europe/Warsaw, whatever the device zone.

const warsawParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Europe/Warsaw',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** The calendar date ("YYYY-MM-DD") of an instant in Warsaw. */
export function warsawDate(instant: Date): string {
  const parts = Object.fromEntries(warsawParts.formatToParts(instant).map((part) => [part.type, part.value]))
  return `${parts.year}-${parts.month}-${parts.day}`
}

const dayNumber = (isoDate: string) => {
  const [year, month, day] = isoDate.split('-').map(Number)
  return Date.UTC(year, month - 1, day) / 86_400_000
}

/** Whole calendar days from `from` to `to` (both "YYYY-MM-DD"); negative when `to` is earlier. */
export function daysBetween(from: string, to: string): number {
  return Math.round(dayNumber(to) - dayNumber(from))
}

/** "1 dzień", otherwise "N dni" (Polish has no other form for this noun with a number: 2 dni, 5 dni, 22 dni). */
export function daysLabel(days: number): string {
  return `${days} ${Math.abs(days) === 1 ? 'dzień' : 'dni'}`
}
