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
    lead: 'Krążą fałszywe wiadomości o odszkodowaniach za opóźnione pociągi. Oto, jak je rozpoznać.',
    body: <PrepList items={safety} />,
  },
]

const last = slides.length - 1

/**
 * "Zanim zaczniesz": the rules, what to prepare and the safety notes as one tabbed carousel (WAI-ARIA Tabs).
 *
 * - The track is a native horizontal scroller with mandatory snapping, so a phone swipe is the browser's own physics.
 *   Tabs, arrows, dots and autoplay move it with a short scrollLeft tween (snapping is switched off for those 280 ms,
 *   instantly under reduced motion); any touch, pointer or horizontal wheel on the track cancels the tween.
 * - Height: the slides sit in one flex row, so the track always has the tallest slide's height. Shorter slides leave
 *   space below, but nothing under the carousel moves while swiping (animating the height would make the page jump
 *   right after every snap).
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
      const to = index * track.clientWidth
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

  // A swipe moves the track itself: follow it to keep the tab bar in sync (ignored while our own tween runs).
  function onScroll() {
    const track = trackRef.current
    if (!track || sliding.current || track.clientWidth === 0) return
    const index = Math.max(0, Math.min(last, Math.round(track.scrollLeft / track.clientWidth)))
    if (index !== active) setActive(index)
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
                tabIndex={index === active ? 0 : -1}
                className="guide__tab"
                onClick={() => go(index, true)}
                onKeyDown={onTabKeyDown}
              >
                {slide.title}
              </button>
            ))}
          </div>

          <div
            ref={trackRef}
            id={trackId}
            className="guide__track"
            aria-live={autoplay ? 'off' : 'polite'}
            onScroll={onScroll}
            onPointerDown={takeOver}
            onTouchStart={takeOver}
            onWheel={(event) => {
              if (Math.abs(event.deltaX) > Math.abs(event.deltaY)) takeOver()
            }}
          >
            {slides.map((slide, index) => (
              <div
                key={slide.key}
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
