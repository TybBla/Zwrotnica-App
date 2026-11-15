import { describe, expect, it } from 'vitest'
import { deadlineCalendar, escapeIcsText, foldIcsLine } from './calendar'

const input = {
  caseId: 7,
  carrier: 'PKP Intercity',
  route: 'Gdańsk Główny → Warszawa Centralna',
  travelDate: '2026-10-20',
  // 23:30 UTC on 13.12 is already 14.12 in Warsaw.
  responseDueDate: '2026-12-13T23:30:00Z',
  caseUrl: 'https://zwrotnica.example/sprawy/7',
  now: new Date('2026-11-14T09:05:07Z'),
}

/** Unfolded content lines, so assertions do not depend on where long lines were wrapped. */
const lines = (ics: string) => ics.replace(/\r\n /g, '').split('\r\n')

describe('escapeIcsText', () => {
  it('escapes backslashes, commas, semicolons and new lines', () => {
    expect(escapeIcsText('a\\b, c; d\ne\r\nf')).toBe('a\\\\b\\, c\\; d\\ne\\nf')
  })
})

describe('foldIcsLine', () => {
  it('keeps lines of up to 75 octets and folds longer ones with CRLF and a space', () => {
    expect(foldIcsLine('x'.repeat(75))).toBe('x'.repeat(75))
    const folded = foldIcsLine('x'.repeat(160))
    expect(folded.split('\r\n ').map((part) => part.length)).toEqual([75, 74, 11])
  })

  it('never splits a multi-byte character', () => {
    const folded = foldIcsLine('ą'.repeat(50)) // 100 octets
    const encoder = new TextEncoder()
    for (const [index, part] of folded.split('\r\n').entries()) {
      const text = index === 0 ? part : part.slice(1)
      expect(text).toMatch(/^ą+$/)
      expect(encoder.encode(part).length).toBeLessThanOrEqual(75)
    }
  })
})

describe('deadlineCalendar', () => {
  it('is an all-day event on the Warsaw date of the deadline with CRLF line endings', () => {
    const { content, fileName } = deadlineCalendar(input)

    expect(fileName).toBe('termin-sprawy-7.ics')
    expect(content.startsWith('BEGIN:VCALENDAR\r\n')).toBe(true)
    expect(content.endsWith('END:VCALENDAR\r\n')).toBe(true)
    expect(content.replace(/\r\n/g, '')).not.toContain('\n')
    const all = lines(content)
    expect(all).toContain('VERSION:2.0')
    expect(all).toContain('DTSTART;VALUE=DATE:20261214')
    expect(all).toContain('DTEND;VALUE=DATE:20261215')
    expect(all).toContain('DTSTAMP:20261114T090507Z')
    expect(all).toContain('UID:sprawa-7-termin-odpowiedzi@zwrotnica')
    expect(all).toContain('SEQUENCE:0')
    expect(all).toContain('SUMMARY:Termin odpowiedzi przewoźnika – PKP Intercity')
    expect(all).toContain('URL:https://zwrotnica.example/sprawy/7')
  })

  it('describes the case and the next steps, escaped', () => {
    const description = lines(deadlineCalendar(input).content).find((line) => line.startsWith('DESCRIPTION:'))!

    expect(description).toContain('Gdańsk Główny → Warszawa Centralna\\, przejazd 20.10.2026')
    expect(description).toContain('ponaglenie')
    expect(description).toContain('skargę do Prezesa UTK')
    expect(description).toContain('\\nhttps://zwrotnica.example/sprawy/7')
  })

  it('keeps the same UID for the same case and rolls the end date over a month', () => {
    const first = lines(deadlineCalendar(input).content)
    const second = lines(deadlineCalendar({ ...input, responseDueDate: '2026-12-31T10:00:00Z', now: new Date() }).content)

    expect(second.find((l) => l.startsWith('UID:'))).toBe(first.find((l) => l.startsWith('UID:')))
    expect(second).toContain('DTSTART;VALUE=DATE:20261231')
    expect(second).toContain('DTEND;VALUE=DATE:20270101')
  })

  it('escapes a typed-in carrier name and leaves out an unknown route', () => {
    const all = lines(deadlineCalendar({ ...input, carrier: 'Kolej; Testowa, Sp.', route: null }).content)

    expect(all).toContain('SUMMARY:Termin odpowiedzi przewoźnika – Kolej\\; Testowa\\, Sp.')
    expect(all.find((line) => line.startsWith('DESCRIPTION:'))).toContain('Przejazd 20.10.2026')
  })
})
