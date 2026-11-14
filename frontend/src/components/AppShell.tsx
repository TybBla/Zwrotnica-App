import type { ReactNode } from 'react'
import { ThemeToggle } from './ThemeToggle'
import './AppShell.css'

interface AppShellProps {
  /** Right side of the header, e.g. <AccountMenu />. */
  account?: ReactNode
  /** Called when the wordmark is pressed (goes home once routing exists). */
  onHome?: () => void
  /** Page footer (legal note, sources). */
  footer?: ReactNode
  /** A non-blocking line right under the header (e.g. "Brak połączenia z serwerem"). */
  notice?: ReactNode
  children: ReactNode
}

export function AppShell({ account, onHome, footer, notice, children }: AppShellProps) {
  return (
    <div className="shell">
      <a className="shell__skip" href="#main">
        Przejdź do treści
      </a>
      <header className="shell__header">
        <div className="shell__bar container container--wide">
          <a
            className="wordmark"
            href="/"
            onClick={(event) => {
              if (!onHome) return
              event.preventDefault()
              onHome()
            }}
          >
            <Logo />
            <span className="wordmark__text">
              Refund<span className="wordmark__accent"> Pilot</span>
            </span>
          </a>
          <div className="shell__actions">
            <ThemeToggle />
            {account}
          </div>
        </div>
      </header>
      {notice}
      <main id="main" className="shell__main" tabIndex={-1}>
        {children}
      </main>
      {footer && <footer className="shell__footer">{footer}</footer>}
    </div>
  )
}

/** A navy station plate with a signal-yellow return arrow: the train trip that pays you back. */
function Logo() {
  return (
    <svg className="wordmark__logo" width="30" height="30" viewBox="0 0 28 28" aria-hidden="true" focusable="false">
      <rect width="28" height="28" rx="6" fill="#163a70" />
      <path d="M8 19.5V12a4 4 0 0 1 4-4h6.5" fill="none" stroke="#ffc20e" strokeWidth="2.5" strokeLinecap="round" />
      <path d="m16 5 3.25 3L16 11" fill="none" stroke="#ffc20e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="19" cy="18.5" r="2" fill="#ffffff" />
    </svg>
  )
}
