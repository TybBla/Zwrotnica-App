import { useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { LoginForm } from './LoginForm'
import { RegisterForm } from './RegisterForm'
import './Auth.css'

export type Tab = 'login' | 'register'

const tabs: { id: Tab; label: string }[] = [
  { id: 'login', label: 'Logowanie' },
  { id: 'register', label: 'Rejestracja' },
]

interface AuthPanelProps {
  title: string
  description?: ReactNode
  /** Called once the user is signed in (after registering, too). */
  onSuccess?: () => void
  /** Dialog: focus the e-mail field. Inline: focus the heading, so the context is read first. */
  focus?: 'email' | 'heading'
  /** Id for the heading, so a dialog can be labelled by it. */
  titleId?: string
  /** Inline panels are a labelled region; inside a dialog the dialog itself is the landmark. */
  as?: 'section' | 'div'
  /** The sign-in pages use the panel title as the page's h1. */
  titleAs?: 'h1' | 'h2' | 'h3'
  /** /rejestracja opens on the registration tab. */
  initialTab?: Tab
}

/** Sign in or create an account (tabs share the e-mail). Used inline in the wizard, in the header dialog and on /logowanie. */
export function AuthPanel({ title, description, onSuccess, focus = 'heading', titleId, as: Element = 'section', titleAs, initialTab = 'login' }: AuthPanelProps) {
  // Inside a dialog the panel title is the dialog's own heading.
  const Heading = titleAs ?? (Element === 'div' ? 'h2' : 'h3')
  const [tab, setTab] = useState<Tab>(initialTab)
  const [email, setEmail] = useState('')
  const id = useId()
  const headingId = titleId ?? `${id}-title`
  const headingRef = useRef<HTMLHeadingElement>(null)
  const tabRefs = useRef<Record<Tab, HTMLButtonElement | null>>({ login: null, register: null })

  useLayoutEffect(() => {
    if (focus !== 'heading') return
    const heading = headingRef.current
    if (!heading) return
    heading.focus({ preventScroll: true })
    heading.scrollIntoView?.({ block: 'start' })
  }, [focus])

  // Arrow keys move between tabs (WAI-ARIA tabs pattern, automatic activation).
  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
    event.preventDefault()
    const next: Tab = event.key === 'Home' ? 'login' : event.key === 'End' ? 'register' : tab === 'login' ? 'register' : 'login'
    setTab(next)
    tabRefs.current[next]?.focus()
  }

  const formProps = { email, onEmailChange: setEmail, autoFocus: focus === 'email', onSuccess }

  return (
    <Element className="auth-panel" aria-labelledby={Element === 'section' ? headingId : undefined}>
      <div className="auth-panel__head">
        <Heading className="auth-panel__title" id={headingId} ref={headingRef} tabIndex={-1}>
          {title}
        </Heading>
        {description && <p className="auth-panel__description">{description}</p>}
      </div>
      <div className="auth-tabs" role="tablist" aria-label="Logowanie lub rejestracja">
        {tabs.map((item) => (
          <button
            key={item.id}
            ref={(node) => {
              tabRefs.current[item.id] = node
            }}
            type="button"
            role="tab"
            id={`${id}-tab-${item.id}`}
            aria-selected={tab === item.id}
            aria-controls={`${id}-panel`}
            tabIndex={tab === item.id ? 0 : -1}
            className="auth-tabs__tab"
            onClick={() => setTab(item.id)}
            onKeyDown={onTabKeyDown}
          >
            {item.label}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-tab-${tab}`}>
        {tab === 'login' ? <LoginForm key="login" {...formProps} /> : <RegisterForm key="register" {...formProps} />}
      </div>
    </Element>
  )
}
