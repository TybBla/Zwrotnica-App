import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Tooltip } from './Tooltip'

const TEXT = 'Podaj kwotę, którą faktycznie zapłaciłeś.'

function setup() {
  const user = userEvent.setup()
  render(
    <div>
      <Tooltip label="Więcej o cenie biletu" content={TEXT} />
      <button type="button">Dalej</button>
    </div>,
  )
  const trigger = screen.getByRole('button', { name: 'Więcej o cenie biletu' })
  return { user, trigger }
}

/** The bubble element, shown or not: it is always rendered and always describes the trigger. */
function bubbleOf(trigger: HTMLElement) {
  const id = trigger.getAttribute('aria-describedby')
  expect(id).toBeTruthy()
  const bubble = document.getElementById(id!)
  expect(bubble).not.toBeNull()
  return bubble!
}

function expectOpen(trigger: HTMLElement) {
  const tooltip = screen.getByRole('tooltip')
  expect(tooltip).toBe(bubbleOf(trigger))
  expect(tooltip.textContent).toBe(TEXT)
  expect(tooltip.hidden).toBe(false)
}

function expectClosed(trigger: HTMLElement) {
  expect(screen.queryByRole('tooltip')).toBeNull()
  expect(bubbleOf(trigger).hidden).toBe(true)
}

describe('Tooltip', () => {
  it('is closed initially, but the hint already describes the trigger', () => {
    const { trigger } = setup()
    expectClosed(trigger)
    // A screen reader announces the hint with the button even before it is shown.
    expect(bubbleOf(trigger).textContent).toBe(TEXT)
  })

  it('opens on hover and closes when the pointer leaves', async () => {
    const { user, trigger } = setup()

    await user.hover(trigger)
    await screen.findByRole('tooltip')
    expectOpen(trigger)

    await user.unhover(trigger)
    await waitFor(() => expectClosed(trigger))
  })

  it('stays open while the pointer moves from the trigger onto the bubble (WCAG 1.4.13)', async () => {
    const { user, trigger } = setup()

    await user.hover(trigger)
    const bubble = await screen.findByRole('tooltip')
    await user.hover(bubble)
    await new Promise((resolve) => setTimeout(resolve, 250))

    expect(screen.getByRole('tooltip')).toBe(bubble)

    await user.unhover(bubble)
    await waitFor(() => expectClosed(trigger))
  })

  it('opens on keyboard focus and closes on Escape, keeping aria-describedby', async () => {
    const { user, trigger } = setup()
    const describedBy = trigger.getAttribute('aria-describedby')

    await user.tab()
    expect(document.activeElement).toBe(trigger)
    expectOpen(trigger)

    await user.keyboard('{Escape}')
    expectClosed(trigger)
    expect(trigger.getAttribute('aria-describedby')).toBe(describedBy)
  })

  it('toggles on tap and closes on a tap outside', async () => {
    const { user, trigger } = setup()

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expectOpen(trigger)

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expectClosed(trigger)

    await user.pointer({ keys: '[TouchA]', target: trigger })
    expectOpen(trigger)
    await user.pointer({ keys: '[TouchA]', target: document.body })
    expectClosed(trigger)
  })

  it('pins on a mouse click and unpins on the next click', async () => {
    const { user, trigger } = setup()

    await user.click(trigger)
    expectOpen(trigger)
    // Pinned: leaving with the pointer does not close it.
    await user.unhover(trigger)
    await new Promise((resolve) => setTimeout(resolve, 250))
    expectOpen(trigger)

    await user.click(trigger)
    expectClosed(trigger)
  })

  it('pins a hover-opened hint on click instead of closing it', async () => {
    const { user, trigger } = setup()

    await user.hover(trigger)
    await screen.findByRole('tooltip')
    await user.click(trigger)
    await user.unhover(trigger)
    await new Promise((resolve) => setTimeout(resolve, 250))
    expectOpen(trigger)
  })

  it('inside a dialog, Escape closes only the hint, not the dialog', async () => {
    const user = userEvent.setup()
    const onDialogKeyDown = vi.fn()
    render(
      // A stand-in for AuthDialog / ConfirmDialog, which close on Escape from their own onKeyDown.
      <div role="dialog" aria-label="Logowanie" onKeyDown={(event) => onDialogKeyDown(event.key)}>
        <Tooltip label="Więcej o haśle" content="Co najmniej 6 znaków." />
      </div>,
    )
    const trigger = screen.getByRole('button', { name: 'Więcej o haśle' })

    await user.tab()
    expect(screen.getByRole('tooltip')).toBeTruthy()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('tooltip')).toBeNull()
    expect(onDialogKeyDown).not.toHaveBeenCalledWith('Escape')

    // With the hint closed, Escape reaches the dialog again.
    await user.keyboard('{Escape}')
    expect(onDialogKeyDown).toHaveBeenCalledWith('Escape')
    expect(document.activeElement).toBe(trigger)
  })
})
