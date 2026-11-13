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
 * ⓘ hint that works with every input: hover (mouse, short delay), keyboard focus, and tap (toggles).
 * A tap or click pins it open until a tap outside, blur, or Escape.
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
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') hide()
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) hide()
    }
    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
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
        aria-describedby={open ? id : undefined}
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
          const isTouch = lastPointerType.current === 'touch' || lastPointerType.current === 'pen'
          lastPointerType.current = null
          if (isTouch && open) hide()
          else show(true)
        }}
      >
        {children ?? <InfoIcon size={18} />}
      </button>
      {open && (
        <span ref={bubbleRef} id={id} role="tooltip" className="tooltip__bubble" data-instant={instant || undefined}>
          {content}
        </span>
      )}
    </span>
  )
}
