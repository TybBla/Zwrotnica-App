import { warsawDate } from './deadline'

// iCalendar (RFC 5545) for one all-day event, built on the device: nothing is sent to the server.

export interface DeadlineEventInput {
  caseId: number
  carrier: string
  /** "Gdańsk Główny → Warszawa Centralna", or null when a station is unknown. */
  route: string | null
  /** "YYYY-MM-DD". */
  travelDate: string
  /** The stored deadline instant (UTC); the event falls on its Warsaw calendar date. */
  responseDueDate: string
  caseUrl: string
  now?: Date
}

const MAX_OCTETS = 75
const encoder = new TextEncoder()

/** TEXT value escaping: backslash, semicolon, comma and line breaks. */
export function escapeIcsText(value: string): string {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,')
    .replace(/\r\n|\r|\n/g, '\\n')
}

/** Folds a content line at 75 octets (continuation lines start with a space), never inside a UTF-8 character. */
export function foldIcsLine(line: string): string {
  const parts: string[] = []
  let current = ''
  let octets = 0
  for (const char of line) {
    const size = encoder.encode(char).length
    // A continuation line's leading space counts towards its 75 octets.
    const limit = parts.length === 0 ? MAX_OCTETS : MAX_OCTETS - 1
    if (octets + size > limit) {
      parts.push(current)
      current = ''
      octets = 0
    }
    current += char
    octets += size
  }
  parts.push(current)
  return parts.join('\r\n ')
}

const compact = (isoDate: string) => isoDate.replace(/-/g, '')

/** "2026-10-20" → "20.10.2026". */
const formatShortDate = (isoDate: string) => isoDate.split('-').reverse().join('.')

/** The calendar day after "YYYY-MM-DD" (DTEND of an all-day event is exclusive). */
function nextDay(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day + 1)).toISOString().slice(0, 10)
}

/** "20261114T090507Z". */
const utcStamp = (instant: Date) => instant.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '')

/** The carrier's response deadline as a calendar file: same UID per case, so importing it again updates the event. */
export function deadlineCalendar(input: DeadlineEventInput): { content: string; fileName: string } {
  const due = warsawDate(new Date(input.responseDueDate))
  const trip = input.route ? `${input.route}, przejazd ${formatShortDate(input.travelDate)}` : `Przejazd ${formatShortDate(input.travelDate)}`
  const description = [
    `Sprawa w Zwrotnicy: ${trip}.`,
    'Jeśli przewoźnik nie odpowie do tego dnia, możesz wysłać mu ponaglenie albo złożyć skargę do Prezesa UTK – oba pisma przygotujesz w sprawie.',
    input.caseUrl,
  ].join('\n')

  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Zwrotnica//Termin odpowiedzi przewoznika//PL',
    'CALSCALE:GREGORIAN',
    'METHOD:PUBLISH',
    'BEGIN:VEVENT',
    `UID:sprawa-${input.caseId}-termin-odpowiedzi@zwrotnica`,
    `DTSTAMP:${utcStamp(input.now ?? new Date())}`,
    `DTSTART;VALUE=DATE:${compact(due)}`,
    `DTEND;VALUE=DATE:${compact(nextDay(due))}`,
    `SUMMARY:${escapeIcsText(`Termin odpowiedzi przewoźnika – ${input.carrier}`)}`,
    `DESCRIPTION:${escapeIcsText(description)}`,
    `URL:${input.caseUrl}`,
    'TRANSP:TRANSPARENT',
    'END:VEVENT',
    'END:VCALENDAR',
  ]
  return { content: lines.map(foldIcsLine).join('\r\n') + '\r\n', fileName: `termin-sprawy-${input.caseId}.ics` }
}
