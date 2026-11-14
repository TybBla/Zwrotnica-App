import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AssessmentDto } from '../api/types'
import { VerdictCard } from './VerdictCard'

const base: AssessmentDto = {
  verdict: 'Eligible',
  reason: '50% ceny zapłaconego biletu.',
  compensationAmount: 74.95,
  percent: 50,
  belowMinimum: false,
  priceRequired: false,
  refusalRisk: false,
  outcome: null,
}

beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('reduce'),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }))
})

describe('VerdictCard', () => {
  it('shows "Należy się" with the amount and the reason', () => {
    render(<VerdictCard assessment={base} />)
    expect(screen.getByRole('heading', { name: 'Należy się' })).toBeTruthy()
    expect(screen.getByText('74,95').textContent).toBe('74,95 zł')
    expect(screen.getByText('50% ceny zapłaconego biletu.')).toBeTruthy()
  })

  it('shows "Nie należy się" and "Do sprawdzenia" without an amount', () => {
    const { rerender } = render(<VerdictCard assessment={{ ...base, verdict: 'NotCovered', compensationAmount: 0, reason: 'Pociąg regionalny.' }} />)
    expect(screen.getByRole('heading', { name: 'Nie należy się' })).toBeTruthy()
    expect(screen.queryByText(/zł$/)).toBeNull()

    rerender(<VerdictCard assessment={{ ...base, verdict: 'Unknown', compensationAmount: 0, reason: 'Bilety okresowe…' }} />)
    expect(screen.getByRole('heading', { name: 'Do sprawdzenia' })).toBeTruthy()
  })

  it('warns when the amount is below the minimum payout', () => {
    render(<VerdictCard assessment={{ ...base, compensationAmount: 12.48, belowMinimum: true }} />)
    expect(screen.getByText(/poniżej progu/i).textContent).toMatch(/16 zł/)
  })


  it('never shows the below-minimum notice for a cancellation refund', () => {
    render(<VerdictCard assessment={{ ...base, percent: 100, compensationAmount: 10, belowMinimum: true }} cancelled />)
    expect(screen.queryByText(/poniżej progu/i)).toBeNull()
  })

  it('uses the minimum payout from the reference data when given', () => {
    render(<VerdictCard assessment={{ ...base, compensationAmount: 12.48, belowMinimum: true }} minimumPayout={18.5} />)
    expect(screen.getByText(/poniżej progu/i).textContent).toMatch(/18,5 zł/)
  })

  it('labels a cancellation amount as a ticket refund', () => {
    const { rerender } = render(<VerdictCard assessment={base} />)
    expect(screen.getByText('Kwota odszkodowania')).toBeTruthy()

    rerender(<VerdictCard assessment={{ ...base, percent: 100 }} cancelled />)
    expect(screen.getByText('Kwota zwrotu ceny biletu')).toBeTruthy()
    expect(screen.queryByText('Kwota odszkodowania')).toBeNull()
  })

  it('asks for the price when it is required', () => {
    render(<VerdictCard assessment={{ ...base, compensationAmount: 0, priceRequired: true }} />)
    expect(screen.getByText(/podaj cenę/i)).toBeTruthy()
  })

  it('warns about a possible refusal (force majeure)', () => {
    render(<VerdictCard assessment={{ ...base, refusalRisk: true }} />)
    expect(screen.getByRole('note').textContent).toMatch(/siła wyższa/i)
  })

  it('treats a covered train with less than 60 minutes as not due', () => {
    render(<VerdictCard assessment={{ ...base, compensationAmount: 0, percent: 0, reason: 'Opóźnienie poniżej 60 minut…' }} />)
    expect(screen.getByRole('heading', { name: 'Nie należy się' })).toBeTruthy()
  })
})
