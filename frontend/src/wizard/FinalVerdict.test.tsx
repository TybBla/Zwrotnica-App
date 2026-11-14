import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { OutcomeDto } from '../api/types'
import { FinalVerdict } from './FinalVerdict'

const due: OutcomeDto = {
  title: 'Przewoźnik jest zobowiązany do wypłaty odszkodowania',
  statement:
    'Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości 25% ceny biletu, tj. 37,48 zł.',
  legalBasis: 'Art. 19 rozporządzenia (UE) 2021/782',
  deadline: 'Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.',
}

describe('FinalVerdict', () => {
  it('states the outcome formally, with legal basis, deadline and the disclaimer', () => {
    render(<FinalVerdict outcome={due} tone="success" />)

    const section = screen.getByRole('region', { name: 'Werdykt końcowy' })
    expect(section.getAttribute('data-tone')).toBe('success')
    expect(screen.getByText(due.title)).toBeTruthy()
    expect(section.textContent).toContain(due.statement)
    expect(screen.getByText('37,48 zł').className).toContain('tabular')
    expect(screen.getByText('Art. 19 rozporządzenia (UE) 2021/782')).toBeTruthy()
    expect(screen.getByText(due.deadline!)).toBeTruthy()
    expect(screen.getByText('Ocena ma charakter informacyjny i nie stanowi porady prawnej.')).toBeTruthy()
  })

  it('leaves out the legal basis and deadline when there are none, and can be a page section', () => {
    render(
      <FinalVerdict
        outcome={{ title: 'Nie można jednoznacznie ustalić uprawnień', statement: 'Powód.', legalBasis: null, deadline: null }}
        tone="warning"
        headingLevel={2}
      />,
    )

    expect(screen.getByRole('heading', { level: 2, name: 'Werdykt końcowy' })).toBeTruthy()
    expect(screen.queryByText('Podstawa prawna')).toBeNull()
    expect(screen.queryByText('Termin')).toBeNull()
  })
})
