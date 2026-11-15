import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { ChevronLeftIcon, ChevronRightIcon } from '../components/icons'
import { useMediaQuery } from '../hooks/useMediaQuery'
import { preparation, rules, safety, type PrepItem } from './content'

/** How long a slide stays before autoplay moves on. */
export const AUTOPLAY_MS = 12_000
/** Programmatic slide (tab, arrow, dot, autoplay): short ease-out, in the 250–300 ms range. */
const SLIDE_MS = 280
/** Ease-out quart: close to the --ease-out token (fast start, gentle settle). */
const easeOut = (p: number) => 1 - (1 - p) ** 4

interface Slide {
  key: string
  title: string
  /** Visible tab label on narrow screens when the title would not fit; the title stays the accessible name. */
  short?: string
  lead: string
  body: ReactNode
}

const slides: Slide[] = [
  {
    key: 'zasady',
    title: 'Ile możesz odzyskać',
    lead: 'Zasady z rozporządzenia (UE) 2021/782 w skrócie.',
    body: (
      <>
        <RulesTable />
        <p className="home-note">
          Procent liczy się od ceny faktycznie zapłaconej, czyli po uldze. Kwot poniżej progu przewoźnik może nie
          wypłacić. W PKP Intercity próg wynosi 16 zł.
        </p>
      </>
    ),
  },
  {
    key: 'co-przygotowac',
    title: 'Co przygotować',
    lead: 'Zanim zaczniesz, miej pod ręką:',
    body: <PrepList items={preparation} />,
  },
  {
    key: 'bezpieczenstwo',
    title: 'Bezpieczeństwo i oszustwa',
    short: 'Bezpieczeństwo',
    lead: 'Krążą fałszywe wiadomości o odszkodowaniach za opóźnione pociągi. Oto, jak je rozpoznać.',
    body: <PrepList items={safety} />,
  },
]

const last = slides.length - 1

/** Distance between two slide starts (slide width + the gap that keeps a neighbour from peeking in). */
function stride(track: HTMLElement, slides: (HTMLElement | null)[]) {
  const [first, second] = slides
  return first && second ? second.offsetLeft - first.offsetLeft || track.clientWidth : track.clientWidth
}

/**
 * "Zanim zaczniesz": the rules, what to prepare and the safety notes as one tabbed carousel (WAI-ARIA Tabs).
 *
 * - The track is a native horizontal scroller with mandatory snapping, so a phone swipe is the browser's own physics.
 *   Tabs, arrows, dots and autoplay move it with a short scrollLeft tween (snapping is switched off for those 280 ms,
 *   instantly under reduced motion); any touch, pointer or horizontal wheel on the track cancels the tween.
 * - Height: the track follows the active slide's measured height (ResizeObserver, 250 ms ease-out, instant under
 *   reduced motion), so short slides leave no gap. Tabs, arrows, dots and autoplay change it together with the slide;
 *   during a touch swipe the height of the slide being left is kept until the snap settles (scrollend, or a short
 *   quiet period where scrollend is missing), so the content below never jitters under the finger.
 * - Manual navigation has ends (arrows disabled on the first and last slide, arrow keys do not wrap); only autoplay
 *   loops back to the first slide.
 * - Autoplay (12 s) has no pause button by product decision: it pauses on hover, on focus inside, while the tab is
 *   hidden, stops for good after any user navigation, and never runs under prefers-reduced-motion. This is a known
 *   trade-off against WCAG 2.2.2 accepted by the product owner.
 */
export function StartGuide() {
  const id = useId()
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')
  const [active, setActive] = useState(0)
  const [interacted, setInteracted] = useState(false)
  const [hovered, setHovered] = useState(false)
  const [focusedInside, setFocusedInside] = useState(false)
  const [pageHidden, setPageHidden] = useState(() => typeof document !== 'undefined' && document.hidden)

  const trackRef = useRef<HTMLDivElement>(null)
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const tween = useRef<number | null>(null)
  const sliding = useRef(false)
  const slideRefs = useRef<(HTMLDivElement | null)[]>([])
  const touching = useRef(false)
  const settleTimer = useRef<number | undefined>(undefined)
  /** Which slide the track's height follows; lags behind `active` during a swipe. */
  const [heightIndex, setHeightIndex] = useState(0)
  const [heights, setHeights] = useState<number[]>([])

  const autoplay = !reducedMotion && !interacted
  const running = autoplay && !hovered && !focusedInside && !pageHidden

  const stopTween = useCallback(() => {
    if (tween.current !== null) cancelAnimationFrame(tween.current)
    tween.current = null
    sliding.current = false
    delete trackRef.current?.dataset.sliding
  }, [])

  const scrollToSlide = useCallback(
    (index: number) => {
      const track = trackRef.current
      if (!track) return
      stopTween()
      const from = track.scrollLeft
      const to = index * stride(track, slideRefs.current)
      if (reducedMotion || typeof requestAnimationFrame !== 'function' || from === to) {
        track.scrollLeft = to
        return
      }
      sliding.current = true
      track.dataset.sliding = ''
      let start: number | undefined
      const step = (now: number) => {
        start ??= now
        const progress = Math.min(1, (now - start) / SLIDE_MS)
        track.scrollLeft = from + (to - from) * easeOut(progress)
        if (progress < 1) tween.current = requestAnimationFrame(step)
        else stopTween()
      }
      tween.current = requestAnimationFrame(step)
    },
    [reducedMotion, stopTween],
  )

  const go = useCallback(
    (index: number, byUser: boolean) => {
      const next = Math.max(0, Math.min(last, index))
      if (byUser) setInteracted(true)
      setActive(next)
      setHeightIndex(next)
      scrollToSlide(next)
    },
    [scrollToSlide],
  )

  useEffect(() => {
    if (!running) return
    const timer = window.setTimeout(() => go(active === last ? 0 : active + 1, false), AUTOPLAY_MS)
    return () => window.clearTimeout(timer)
  }, [running, active, go])

  useEffect(() => {
    const onVisibility = () => setPageHidden(document.hidden)
    document.addEventListener('visibilitychange', onVisibility)
    return () => document.removeEventListener('visibilitychange', onVisibility)
  }, [])

  useEffect(() => stopTween, [stopTween])

  // Measure every slide (content reflows with width and font size); jsdom and old browsers keep the natural height.
  useEffect(() => {
    if (typeof ResizeObserver !== 'function') return
    const observer = new ResizeObserver(() => {
      const next = slideRefs.current.map((slide) => slide?.offsetHeight ?? 0)
      setHeights((prev) => (prev.length === next.length && prev.every((h, i) => h === next[i]) ? prev : next))
    })
    slideRefs.current.forEach((slide) => slide && observer.observe(slide))
    return () => observer.disconnect()
  }, [])

  useEffect(() => () => window.clearTimeout(settleTimer.current), [])

  const indexFromScroll = () => {
    const track = trackRef.current
    if (!track || track.clientWidth === 0) return null
    return Math.max(0, Math.min(last, Math.round(track.scrollLeft / stride(track, slideRefs.current))))
  }

  // The swipe has come to rest: now the height may follow the slide that snapped into place.
  function settle() {
    window.clearTimeout(settleTimer.current)
    if (touching.current || sliding.current) return
    const index = indexFromScroll()
    if (index !== null) setHeightIndex(index)
  }

  function settleSoon() {
    window.clearTimeout(settleTimer.current)
    settleTimer.current = window.setTimeout(settle, 150)
  }

  // A swipe moves the track itself: follow it to keep the tab bar in sync (ignored while our own tween runs).
  function onScroll() {
    if (sliding.current) return
    const index = indexFromScroll()
    if (index === null) return
    if (index !== active) setActive(index)
    settleSoon()
  }

  // The user takes over the track: drop our tween so the browser's snapping (and the finger) win.
  function takeOver() {
    setInteracted(true)
    if (sliding.current) stopTween()
  }

  function onTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const target =
      event.key === 'ArrowRight'
        ? Math.min(last, active + 1)
        : event.key === 'ArrowLeft'
          ? Math.max(0, active - 1)
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? last
              : null
    if (target === null) return
    event.preventDefault()
    go(target, true)
    tabRefs.current[target]?.focus()
  }

  const trackId = `${id}-track`

  return (
    <section className="home-section" aria-labelledby={`${id}-title`}>
      <div className="container container--wide home-section__grid">
        <header className="home-section__head">
          <h2 className="home-section__title" id={`${id}-title`}>
            Zanim zaczniesz
          </h2>
        </header>

        <div
          className="home-section__body guide"
          onPointerEnter={(event) => event.pointerType !== 'touch' && setHovered(true)}
          onPointerLeave={() => setHovered(false)}
          onFocus={() => setFocusedInside(true)}
          onBlur={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setFocusedInside(false)
          }}
        >
          <div className="guide__tabs" role="tablist" aria-label="Zanim zaczniesz">
            {slides.map((slide, index) => (
              <button
                key={slide.key}
                ref={(node) => {
                  tabRefs.current[index] = node
                }}
                type="button"
                role="tab"
                id={`${id}-tab-${slide.key}`}
                aria-selected={index === active}
                aria-controls={`${id}-panel-${slide.key}`}
                aria-label={slide.short ? slide.title : undefined}
                tabIndex={index === active ? 0 : -1}
                className="guide__tab"
                onClick={() => go(index, true)}
                onKeyDown={onTabKeyDown}
              >
                {slide.short ? (
                  <>
                    <span className="guide__tab-full">{slide.title}</span>
                    <span className="guide__tab-short" aria-hidden="true">
                      {slide.short}
                    </span>
                  </>
                ) : (
                  slide.title
                )}
              </button>
            ))}
          </div>

          <div
            ref={trackRef}
            id={trackId}
            className="guide__track"
            aria-live={autoplay ? 'off' : 'polite'}
            style={heights[heightIndex] ? { height: heights[heightIndex] } : undefined}
            onScroll={onScroll}
            onScrollEnd={settle}
            onPointerDown={takeOver}
            onTouchStart={() => {
              touching.current = true
              takeOver()
            }}
            onTouchEnd={() => {
              touching.current = false
              settleSoon()
            }}
            onTouchCancel={() => {
              touching.current = false
              settleSoon()
            }}
            onWheel={(event) => {
              if (Math.abs(event.deltaX) > Math.abs(event.deltaY)) takeOver()
            }}
          >
            {slides.map((slide, index) => (
              <div
                key={slide.key}
                ref={(node) => {
                  slideRefs.current[index] = node
                }}
                role="tabpanel"
                id={`${id}-panel-${slide.key}`}
                aria-labelledby={`${id}-tab-${slide.key}`}
                tabIndex={index === active ? 0 : undefined}
                inert={index !== active}
                className="guide__slide"
              >
                <h3 className="visually-hidden">{slide.title}</h3>
                <p className="home-section__lead">{slide.lead}</p>
                {slide.body}
              </div>
            ))}
          </div>

          <div className="guide__controls">
            {/* Pointer shortcut only: the tabs above already give screen reader and keyboard users the same choice. */}
            <div className="guide__dots" aria-hidden="true">
              {slides.map((slide, index) => (
                <button
                  key={slide.key}
                  type="button"
                  tabIndex={-1}
                  className="guide__dot"
                  data-active={index === active || undefined}
                  onClick={() => go(index, true)}
                />
              ))}
            </div>
            <div className="guide__arrows">
              <button
                type="button"
                className="guide__arrow"
                aria-label="Poprzedni temat"
                aria-controls={trackId}
                aria-disabled={active === 0}
                onClick={() => active > 0 && go(active - 1, true)}
              >
                <ChevronLeftIcon />
              </button>
              <button
                type="button"
                className="guide__arrow"
                aria-label="Następny temat"
                aria-controls={trackId}
                aria-disabled={active === last}
                onClick={() => active < last && go(active + 1, true)}
              >
                <ChevronRightIcon />
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}

function PrepList({ items }: { items: PrepItem[] }) {
  return (
    <dl className="prep-list">
      {items.map((item) => (
        <div key={item.title} className="prep-list__item">
          <dt className="prep-list__title">
            {item.title}
            {item.optional && <span className="prep-list__optional">opcjonalnie</span>}
          </dt>
          <dd className="prep-list__text">{item.text}</dd>
        </div>
      ))}
    </dl>
  )
}

function RulesTable() {
  return (
    <div className="rules">
      {/* Explicit roles: on phones the rows are restyled as blocks (display: block), which makes Safari and older
          Chrome drop the native table semantics; the roles keep "row 2 of 6, Sytuacja / Co przysługuje" for readers. */}
      <table className="rules__table" role="table">
        <caption className="visually-hidden">Ile przysługuje za opóźnienie lub odwołanie pociągu</caption>
        <thead role="rowgroup">
          <tr role="row">
            <th scope="col" role="columnheader">
              Sytuacja
            </th>
            <th scope="col" role="columnheader">
              Co przysługuje
            </th>
          </tr>
        </thead>
        <tbody role="rowgroup">
          {rules.map((rule) => (
            <tr key={rule.situation} data-kind={rule.kind} role="row">
              <th scope="row" role="rowheader">
                <span className="rules__situation">{rule.situation}</span>
                {rule.detail && <span className="rules__detail">{rule.detail}</span>}
              </th>
              <td role="cell">
                <span className="rules__outcome">{rule.outcome}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
