import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { InfoIcon } from './icons'
import './Tooltip.css'

interface TooltipProps {
  /** Accessible name of the trigger, e.g. "Więcej o cenie biletu". */
  label: string
  content: ReactNode
  /** Trigger content; defaults to the ⓘ icon. */
  children?: ReactNode
}

const HOVER_DELAY_MS = 120
const HOVER_CLOSE_DELAY_MS = 120
const VIEWPORT_MARGIN = 12

// Once one tooltip was just open, the next opens instantly and without animation (moving along a form).
let lastClosedAt = 0
const recentlyClosed = () => Date.now() - lastClosedAt < 400

/**
 * ⓘ hint that works with every input: hover (mouse, short delay), keyboard focus, and tap.
 * A click or tap pins it open; the next click or tap on the trigger closes it, as do a press outside, blur and Escape.
 *
 * The bubble is always in the DOM (`hidden` while closed), so the trigger's aria-describedby never changes:
 * a screen reader announces the hint with the button whether or not it is shown.
 */
export function Tooltip({ label, content, children }: TooltipProps) {
  const id = useId()
  const [open, setOpen] = useState(false)
  const [instant, setInstant] = useState(false)
  const pinned = useRef(false)
  const lastPointerType = useRef<string | null>(null)
  const hoverTimer = useRef<number | undefined>(undefined)
  const rootRef = useRef<HTMLSpanElement>(null)
  const bubbleRef = useRef<HTMLSpanElement>(null)

  const clearTimer = () => window.clearTimeout(hoverTimer.current)

  const show = (pin: boolean) => {
    clearTimer()
    pinned.current = pinned.current || pin
    setInstant(recentlyClosed())
    setOpen(true)
  }

  const hide = () => {
    clearTimer()
    pinned.current = false
    setOpen((wasOpen) => {
      if (wasOpen) lastClosedAt = Date.now()
      return false
    })
  }

  useEffect(() => clearTimer, [])

  useEffect(() => {
    if (!open) return
    // Capture phase on the document runs before React's own listeners: inside a dialog, Escape closes only the
    // hint (WCAG 1.4.13 "dismissible"), not the dialog around it. preventDefault also stops the native <dialog> cancel.
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      event.preventDefault()
      hide()
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) hide()
    }
    document.addEventListener('keydown', onKeyDown, true)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown, true)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open])

  // Keep the bubble inside the viewport: nudge sideways, flip above when there is no room below.
  useLayoutEffect(() => {
    const bubble = bubbleRef.current
    if (!open || !bubble) return
    bubble.style.setProperty('--nudge', '0px')
    const rect = bubble.getBoundingClientRect()
    const overflowRight = rect.right - (window.innerWidth - VIEWPORT_MARGIN)
    const overflowLeft = VIEWPORT_MARGIN - rect.left
    const nudge = overflowRight > 0 ? -overflowRight : overflowLeft > 0 ? overflowLeft : 0
    bubble.style.setProperty('--nudge', `${nudge}px`)
    bubble.dataset.side = rect.bottom > window.innerHeight - VIEWPORT_MARGIN && rect.top > rect.height + 40 ? 'top' : 'bottom'
  }, [open])

  return (
    // Hover lives on the root, which also contains the bubble, so the pointer can move onto the text (WCAG 1.4.13);
    // a short close delay bridges the gap between trigger and bubble.
    <span
      className="tooltip"
      ref={rootRef}
      data-open={open || undefined}
      onPointerEnter={(event) => {
        if (event.pointerType !== 'mouse') return
        clearTimer()
        if (open) return
        if (recentlyClosed()) show(false)
        else hoverTimer.current = window.setTimeout(() => show(false), HOVER_DELAY_MS)
      }}
      onPointerLeave={(event) => {
        if (event.pointerType !== 'mouse') return
        clearTimer()
        if (!pinned.current) hoverTimer.current = window.setTimeout(hide, HOVER_CLOSE_DELAY_MS)
      }}
    >
      <button
        type="button"
        className="tooltip__trigger"
        aria-label={label}
        aria-describedby={id}
        onPointerDown={(event) => {
          lastPointerType.current = event.pointerType
        }}
        onFocus={() => {
          // Pointer-initiated focus is handled by the click that follows; this branch is the keyboard.
          if (lastPointerType.current === null) show(true)
        }}
        onBlur={() => {
          lastPointerType.current = null
          hide()
        }}
        onClick={() => {
          lastPointerType.current = null
          // Mouse, touch and Enter/Space alike: the first press pins the hint open (a hover-opened one too),
          // the next press on the trigger unpins and closes it.
          if (open && pinned.current) hide()
          else show(true)
        }}
      >
        {children ?? <InfoIcon size={18} />}
      </button>
      <span
        ref={bubbleRef}
        id={id}
        role="tooltip"
        className="tooltip__bubble"
        hidden={!open}
        data-instant={instant || undefined}
      >
        {content}
      </span>
    </span>
  )
}
