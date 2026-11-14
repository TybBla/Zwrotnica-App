import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { ApiError } from '../api/client'
import { Button } from './Button'
import { AlertCircleIcon } from './icons'
import './ConfirmDialog.css'

interface ConfirmDialogProps {
  open: boolean
  title: string
  children: ReactNode
  confirmLabel: string
  /** 'danger' (default) for destructive actions, 'primary' for a question that only asks to confirm. */
  confirmVariant?: 'danger' | 'primary'
  /** May throw; the dialog then stays open and shows the reason. Resolving closes it. */
  onConfirm: () => Promise<void>
  onClose: () => void
  /** Where focus goes when the element that opened the dialog is gone (e.g. the deleted row's button). */
  fallbackFocus?: () => HTMLElement | null
}

/**
 * A destructive question in a native modal <dialog>: focus starts on "Anuluj" (the safe choice), Tab stays inside,
 * Escape and the backdrop cancel, and focus returns to the trigger.
 */
export function ConfirmDialog({ open, ...props }: ConfirmDialogProps) {
  if (!open) return null
  return <OpenConfirmDialog {...props} />
}

const FOCUSABLE = 'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'

function OpenConfirmDialog({ title, children, confirmLabel, confirmVariant = 'danger', onConfirm, onClose, fallbackFocus }: Omit<ConfirmDialogProps, 'open'>) {
  const ref = useRef<HTMLDialogElement>(null)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const id = useId()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const latest = useRef({ onClose, fallbackFocus, busy })
  useEffect(() => {
    latest.current = { onClose, fallbackFocus, busy }
  })

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    const returnTo = document.activeElement instanceof HTMLElement ? document.activeElement : null
    if (typeof dialog.showModal === 'function') {
      if (!dialog.open) dialog.showModal()
    } else {
      dialog.setAttribute('open', '')
    }
    cancelRef.current?.focus()

    const onCancel = (event: Event) => {
      event.preventDefault()
      if (!latest.current.busy) latest.current.onClose()
    }
    dialog.addEventListener('cancel', onCancel)
    return () => {
      dialog.removeEventListener('cancel', onCancel)
      if (dialog.open && typeof dialog.close === 'function') dialog.close()
      const target = returnTo?.isConnected ? returnTo : (latest.current.fallbackFocus?.() ?? document.getElementById('main'))
      target?.focus({ preventScroll: true })
    }
  }, [])

  const confirm = async () => {
    setBusy(true)
    setError(null)
    try {
      await onConfirm()
      onClose()
    } catch (reason) {
      setBusy(false)
      setError(reason instanceof ApiError ? reason.detail : 'Nie udało się. Spróbuj ponownie.')
    }
  }

  const onKeyDown = (event: KeyboardEvent<HTMLDialogElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      if (!busy) onClose()
      return
    }
    if (event.key !== 'Tab') return
    const items = [...event.currentTarget.querySelectorAll<HTMLElement>(FOCUSABLE)]
    if (items.length === 0) return
    const first = items[0]
    const last = items[items.length - 1]
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  return (
    <dialog
      ref={ref}
      className="confirm-dialog"
      aria-labelledby={`${id}-title`}
      aria-describedby={`${id}-body`}
      onKeyDown={onKeyDown}
      onClick={(event) => {
        if (event.target === event.currentTarget && !busy) onClose()
      }}
    >
      <div className="confirm-dialog__content">
        <h2 className="confirm-dialog__title" id={`${id}-title`}>
          {title}
        </h2>
        <div className="confirm-dialog__body" id={`${id}-body`}>
          {children}
        </div>
        {error && (
          <p className="confirm-dialog__error" role="alert">
            <AlertCircleIcon size={18} />
            {error}
          </p>
        )}
        <div className="confirm-dialog__actions">
          <Button ref={cancelRef} variant="secondary" onClick={onClose} disabled={busy}>
            Anuluj
          </Button>
          <Button variant={confirmVariant} onClick={() => void confirm()} loading={busy}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </dialog>
  )
}
