import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { Home } from './Home'
import { faq } from './content'

describe('Home – safety', () => {
  it('explains calmly how to recognise a scam', () => {
    render(
      <MemoryRouter>
        <Home onStart={() => {}} />
      </MemoryRouter>,
    )

    const section = screen.getByRole('region', { name: 'Bezpieczeństwo i oszustwa' })
    const text = section.textContent ?? ''
    expect(text).toContain('Zwrotnica nie jest przewoźnikiem')
    expect(text).toContain('nie wypłaca odszkodowań')
    expect(text).toMatch(/nigdy nie prosi o dane karty, o opłatę ani o logowanie do banku/)
    expect(text).toContain('wysyłasz sam')
    expect(text).toContain('Odszkodowanie wypłaca się na wniosek pasażera – nie trzeba podawać danych pod linkiem z SMS-a, żeby je dostać.')
    expect(text).not.toContain('Przewoźnicy nie wysyłają')
    expect(text).toMatch(/adres strony/)
    expect(text).toMatch(/oficjaln/)
    expect(within(section).queryByRole('link')).toBeNull()
  })

  it('opens the FAQ with the scam question', () => {
    expect(faq[0].question).toBe('Jak rozpoznać oszustwo?')
  })
})
