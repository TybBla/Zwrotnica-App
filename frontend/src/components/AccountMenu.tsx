import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/useAuth'
import { Button } from './Button'
import { LogOutIcon, UserIcon } from './icons'
import './AccountMenu.css'

export interface AccountMenuItem {
  label: string
  icon?: ReactNode
  onSelect: () => void
}

interface AccountMenuProps {
  onSignIn: () => void
  /** Extra entries above "Wyloguj się" (profile, cases — wired up with routing). */
  items?: AccountMenuItem[]
}

/** Header account control: "Zaloguj się" when signed out, otherwise an avatar with a small menu. */
export function AccountMenu({ onSignIn, items = [] }: AccountMenuProps) {
  const { user, loading, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const menuId = useId()
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        triggerRef.current?.focus()
      }
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open])

  if (loading) return <span className="account-menu__placeholder" aria-hidden="true" />

  if (!user) {
    return (
      <Button variant="secondary" size="sm" onClick={onSignIn}>
        Zaloguj się
      </Button>
    )
  }

  const select = (action: () => void) => () => {
    setOpen(false)
    action()
  }

  return (
    <div className="account-menu" ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className="account-menu__trigger"
        aria-label={`Konto: ${user.email}`}
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="account-menu__avatar" aria-hidden="true">
          {user.email.charAt(0).toUpperCase()}
        </span>
      </button>
      {open && (
        <div className="account-menu__panel" id={menuId}>
          <p className="account-menu__email">
            <UserIcon size={16} />
            <span>{user.email}</span>
          </p>
          <ul className="account-menu__list">
            {items.map((item) => (
              <li key={item.label}>
                <button type="button" className="account-menu__item" onClick={select(item.onSelect)}>
                  {item.icon}
                  {item.label}
                </button>
              </li>
            ))}
            <li>
              <button type="button" className="account-menu__item" onClick={select(() => void logout())}>
                <LogOutIcon size={18} />
                Wyloguj się
              </button>
            </li>
          </ul>
        </div>
      )}
    </div>
  )
}
