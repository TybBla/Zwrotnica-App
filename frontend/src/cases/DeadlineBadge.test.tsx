import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DeadlineBadge } from './DeadlineBadge'
import { daysLabel, warsawDate } from '../lib/deadline'

// 14 Nov 2026, 11:00 in Warsaw.
const now = new Date('2026-11-14T10:00:00Z')

describe('DeadlineBadge', () => {
  it('counts down the days left for the carrier to answer a sent complaint', () => {
    render(<DeadlineBadge status="Sent" responseDueDate="2026-12-02T10:00:00Z" now={now} />)
    expect(screen.getByText(/jeszcze 18 dni/)).toBeTruthy()
    expect(screen.getByText(/Odpowiedź do 2 grudnia 2026/)).toBeTruthy()
  })

  it('uses the singular for one day left', () => {
    render(<DeadlineBadge status="Sent" responseDueDate="2026-11-15T09:00:00Z" now={now} />)
    expect(screen.getByText(/jeszcze 1 dzień/)).toBeTruthy()
  })

  it('reads the due date in Warsaw time, not UTC', () => {
    // 23:30 UTC on 1 Dec is already 2 Dec in Warsaw.
    render(<DeadlineBadge status="Sent" responseDueDate="2026-12-01T23:30:00Z" now={now} />)
    expect(screen.getByText(/Odpowiedź do 2 grudnia 2026 · jeszcze 18 dni/)).toBeTruthy()
  })

  it('says the deadline is today on the last day', () => {
    render(<DeadlineBadge status="Sent" responseDueDate="2026-11-14T20:00:00Z" now={now} />)
    expect(screen.getByText(/Odpowiedź do 14 listopada 2026 · termin mija dziś/)).toBeTruthy()
  })

  it('points to UTK once the carrier is past due', () => {
    render(<DeadlineBadge status="Sent" responseDueDate="2026-11-10T10:00:00Z" now={now} />)
    expect(screen.getByText(/Brak odpowiedzi\? Możesz złożyć skargę do UTK/)).toBeTruthy()
    const link = screen.getByRole('link', { name: /UTK/ }) as HTMLAnchorElement
    expect(link.href).toBe('https://utk.gov.pl/')
  })

  it('shows nothing for other statuses', () => {
    for (const status of ['New', 'Paid', 'Rejected'] as const) {
      const { container, unmount } = render(<DeadlineBadge status={status} responseDueDate="2026-12-02T10:00:00Z" now={now} />)
      expect(container.textContent).toBe('')
      unmount()
    }
  })

  it('shows nothing for a sent case without a due date', () => {
    const { container } = render(<DeadlineBadge status="Sent" responseDueDate={null} now={now} />)
    expect(container.textContent).toBe('')
  })
})

describe('deadline helpers', () => {
  it('pluralizes days the Polish way', () => {
    expect(daysLabel(1)).toBe('1 dzień')
    expect(daysLabel(2)).toBe('2 dni')
    expect(daysLabel(4)).toBe('4 dni')
    expect(daysLabel(5)).toBe('5 dni')
    expect(daysLabel(22)).toBe('22 dni')
  })

  it('gives the calendar date in Warsaw', () => {
    expect(warsawDate(new Date('2026-06-30T22:30:00Z'))).toBe('2026-07-01')
    expect(warsawDate(new Date('2026-01-31T22:30:00Z'))).toBe('2026-01-31')
  })
})
