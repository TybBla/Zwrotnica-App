import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AUTOPLAY_MS, StartGuide } from './StartGuide'

const titles = ['Ile możesz odzyskać', 'Co przygotować', 'Bezpieczeństwo i oszustwa']

function setup() {
  render(<StartGuide />)
  const tabs = screen.getAllByRole('tab')
  const panels = screen.getAllByRole('tabpanel')
  const track = panels[0].parentElement as HTMLElement
  const carousel = screen.getByRole('tablist').parentElement as HTMLElement
  const prev = screen.getByRole('button', { name: 'Poprzedni temat' })
  const next = screen.getByRole('button', { name: 'Następny temat' })
  return { tabs, panels, track, carousel, prev, next }
}

type Setup = ReturnType<typeof setup>

const selected = (tabs: HTMLElement[]) => tabs.findIndex((tab) => tab.getAttribute('aria-selected') === 'true')

function stubReducedMotion() {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('prefers-reduced-motion'),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }))
}

afterEach(() => {
  vi.useRealTimers()
})

describe('StartGuide – tabs', () => {
  it('wires the WAI-ARIA tabs pattern', () => {
    const { tabs, panels } = setup()

    expect(screen.getByRole('heading', { level: 2, name: 'Zanim zaczniesz' })).toBeTruthy()
    expect(titles.map((name) => screen.getByRole('tab', { name }))).toEqual(tabs)
    tabs.forEach((tab, index) => {
      expect(tab.getAttribute('aria-controls')).toBe(panels[index].id)
      expect(panels[index].getAttribute('aria-labelledby')).toBe(tab.id)
    })
    expect(tabs.map((tab) => tab.getAttribute('aria-selected'))).toEqual(['true', 'false', 'false'])
    expect(tabs.map((tab) => tab.getAttribute('tabindex'))).toEqual(['0', '-1', '-1'])
    expect(panels.map((panel) => panel.hasAttribute('inert'))).toEqual([false, true, true])
    expect(panels[0].getAttribute('tabindex')).toBe('0')
  })

  it('keeps the full title as the name of a tab with a short label', () => {
    const { tabs } = setup()
    expect(tabs[2].getAttribute('aria-label')).toBe('Bezpieczeństwo i oszustwa')
    expect(tabs[2].querySelector('.guide__tab-short')?.textContent).toBe('Bezpieczeństwo')
    expect(tabs[2].querySelector('.guide__tab-short')?.getAttribute('aria-hidden')).toBe('true')
  })

  it('sizes the track to the active slide', () => {
    const slideHeights: Record<string, number> = { zasady: 600, 'co-przygotowac': 300, bezpieczenstwo: 450 }
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(function (this: HTMLElement) {
      const key = Object.keys(slideHeights).find((k) => this.id.endsWith(`-panel-${k}`))
      return key ? slideHeights[key] : 0
    })
    vi.stubGlobal(
      'ResizeObserver',
      class {
        callback: () => void
        constructor(callback: () => void) {
          this.callback = callback
        }
        observe() {
          this.callback()
        }
        disconnect() {}
      },
    )
    const { tabs, track } = setup()

    expect(track.style.height).toBe('600px')
    fireEvent.click(tabs[1])
    expect(track.style.height).toBe('300px')
    fireEvent.click(tabs[2])
    expect(track.style.height).toBe('450px')
  })

  it('keeps the rules as a table with its headers', () => {
    const { panels } = setup()
    const table = within(panels[0]).getByRole('table')
    expect(within(table).getAllByRole('columnheader').map((th) => th.textContent)).toEqual([
      'Sytuacja',
      'Co przysługuje',
    ])
    expect(within(table).getAllByRole('rowheader')).toHaveLength(5)
  })

  it('switches panels when a tab is clicked', () => {
    const { tabs, panels } = setup()

    fireEvent.click(tabs[2])

    expect(selected(tabs)).toBe(2)
    expect(panels.map((panel) => panel.hasAttribute('inert'))).toEqual([true, true, false])
    expect(panels[2].textContent).toContain('Zwrotnica nie jest przewoźnikiem')
    expect(screen.getByRole('tabpanel', { name: 'Bezpieczeństwo i oszustwa' })).toBe(panels[2])
  })

  it('moves with ArrowLeft/Right, Home and End without wrapping', () => {
    const { tabs } = setup()
    act(() => tabs[0].focus())

    fireEvent.keyDown(tabs[0], { key: 'ArrowLeft' })
    expect(selected(tabs)).toBe(0)

    fireEvent.keyDown(tabs[0], { key: 'ArrowRight' })
    expect(selected(tabs)).toBe(1)
    expect(document.activeElement).toBe(tabs[1])
    expect(tabs[1].getAttribute('tabindex')).toBe('0')
    expect(tabs[0].getAttribute('tabindex')).toBe('-1')

    fireEvent.keyDown(tabs[1], { key: 'End' })
    expect(selected(tabs)).toBe(2)
    expect(document.activeElement).toBe(tabs[2])

    fireEvent.keyDown(tabs[2], { key: 'ArrowRight' })
    expect(selected(tabs)).toBe(2)

    fireEvent.keyDown(tabs[2], { key: 'Home' })
    expect(selected(tabs)).toBe(0)
    expect(document.activeElement).toBe(tabs[0])
  })

  it('disables the arrows at the ends', () => {
    const { tabs, prev, next } = setup()

    expect(prev.getAttribute('aria-disabled')).toBe('true')
    expect(next.getAttribute('aria-disabled')).toBe('false')

    fireEvent.click(prev)
    expect(selected(tabs)).toBe(0)

    fireEvent.click(next)
    fireEvent.click(next)
    expect(selected(tabs)).toBe(2)
    expect(prev.getAttribute('aria-disabled')).toBe('false')
    expect(next.getAttribute('aria-disabled')).toBe('true')

    fireEvent.click(next)
    expect(selected(tabs)).toBe(2)
  })
})

describe('StartGuide – autoplay', () => {
  it('advances every 6 s and loops back to the first slide', () => {
    vi.useFakeTimers()
    const { tabs, track } = setup()
    expect(track.getAttribute('aria-live')).toBe('off')

    act(() => vi.advanceTimersByTime(AUTOPLAY_MS - 1))
    expect(selected(tabs)).toBe(0)
    act(() => vi.advanceTimersByTime(1))
    expect(selected(tabs)).toBe(1)
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS))
    expect(selected(tabs)).toBe(2)
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS))
    expect(selected(tabs)).toBe(0)
  })

  it('pauses while the pointer hovers the carousel', () => {
    vi.useFakeTimers()
    const { tabs, carousel } = setup()

    fireEvent.pointerEnter(carousel, { pointerType: 'mouse' })
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS * 2))
    expect(selected(tabs)).toBe(0)

    fireEvent.pointerLeave(carousel, { pointerType: 'mouse' })
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS))
    expect(selected(tabs)).toBe(1)
  })

  it('pauses while focus is inside the carousel', () => {
    vi.useFakeTimers()
    const outside = document.createElement('button')
    document.body.append(outside)
    const { tabs } = setup()

    act(() => tabs[0].focus())
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS * 2))
    expect(selected(tabs)).toBe(0)

    act(() => outside.focus())
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS))
    expect(selected(tabs)).toBe(1)
    outside.remove()
  })

  it('pauses while the page is hidden', () => {
    vi.useFakeTimers()
    const { tabs } = setup()
    const hidden = vi.spyOn(document, 'hidden', 'get').mockReturnValue(true)

    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS * 2))
    expect(selected(tabs)).toBe(0)

    hidden.mockReturnValue(false)
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS))
    expect(selected(tabs)).toBe(1)
  })

  const interactions: [string, (s: Setup) => void][] = [
    ['a tab click', (s) => fireEvent.click(s.tabs[1])],
    ['an arrow', (s) => fireEvent.click(s.next)],
    ['the keyboard', (s) => fireEvent.keyDown(s.tabs[0], { key: 'ArrowRight' })],
    ['a swipe', (s) => fireEvent.touchStart(s.track)],
    ['a dot', (s) => fireEvent.click(s.carousel.querySelectorAll('.guide__dot')[2])],
  ]

  it.each(interactions)('stops for good after %s', (_, interact) => {
    vi.useFakeTimers()
    const s = setup()

    act(() => interact(s))
    const after = selected(s.tabs)
    // No lingering focus or hover is what holds it.
    act(() => (document.activeElement as HTMLElement | null)?.blur())
    fireEvent.pointerLeave(s.carousel)
    act(() => vi.advanceTimersByTime(AUTOPLAY_MS * 3))

    expect(selected(s.tabs)).toBe(after)
    expect(s.track.getAttribute('aria-live')).toBe('polite')
  })

  it('never runs under prefers-reduced-motion', () => {
    stubReducedMotion()
    vi.useFakeTimers()
    const { tabs, track } = setup()

    act(() => vi.advanceTimersByTime(AUTOPLAY_MS * 3))

    expect(selected(tabs)).toBe(0)
    expect(track.getAttribute('aria-live')).toBe('polite')
  })
})
