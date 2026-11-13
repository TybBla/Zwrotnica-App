import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Tooltip } from './Tooltip'

function setup() {
  const user = userEvent.setup()
  render(
    <div>
      <Tooltip label="Więcej o cenie biletu" content="Podaj kwotę, którą faktycznie zapłaciłeś." />
      <button type="button">Dalej</button>
    </div>,
  )
  const trigger = screen.getByRole('button', { name: 'Więcej o cenie biletu' })
  return { user, trigger }
}

function expectOpenAndDescribing(trigger: HTMLElement, tooltip: HTMLElement) {
  expect(tooltip.textContent).toBe('Podaj kwotę, którą faktycznie zapłaciłeś.')
  expect(trigger.getAttribute('aria-describedby')).toBe(tooltip.id)
}

describe('Tooltip', () => {
  it('is closed initially', () => {
    setup()
    expect(screen.queryByRole('tooltip')).toBeNull()
  })

  it('opens on hover and closes when the pointer leaves', async () => {
    const { user, trigger } = setup()

    await user.hover(trigger)
    expectOpenAndDescribing(trigger, await screen.findByRole('tooltip'))

    await user.unhover(trigger)
    await waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull())
  })

  it('stays open while the pointer moves from the trigger onto the bubble (WCAG 1.4.13)', async () => {
    const { user, trigger } = setup()

    await user.hover(trigger)
    const bubble = await screen.findByRole('tooltip')
    await user.hover(bubble)
    await new Promise((resolve) => setTimeout(resolve, 250))

    expect(screen.getByRole('tooltip')).toBe(bubble)

    await user.unhover(bubble)
    await waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull())
  })

  it('opens on keyboard focus and closes on Escape', async () => {
    const { user, trigger } = setup()

    await user.tab()
    expect(document.activeElement).toBe(trigger)
    expectOpenAndDescribing(trigger, screen.getByRole('tooltip'))

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('tooltip')).toBeNull()
    expect(trigger.getAttribute('aria-describedby')).toBeNull()
  })

  it('toggles on tap and closes on a tap outside', async () => {
    const { user, trigger } = setup()

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expectOpenAndDescribing(trigger, screen.getByRole('tooltip'))

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expect(screen.queryByRole('tooltip')).toBeNull()

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expect(screen.getByRole('tooltip')).toBeTruthy()
    await user.pointer({ keys: '[TouchA]', target: document.body })
    expect(screen.queryByRole('tooltip')).toBeNull()
  })

  it('stays open after a mouse click on the trigger', async () => {
    const { user, trigger } = setup()

    await user.click(trigger)
    expect(screen.getByRole('tooltip')).toBeTruthy()
    await user.unhover(trigger)
    expect(screen.getByRole('tooltip')).toBeTruthy()
  })
})
