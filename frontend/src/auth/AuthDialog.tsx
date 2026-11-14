import { useEffect, useId, useRef, type KeyboardEvent } from 'react'
import { CloseIcon } from '../components/icons'
import { AuthPanel } from './AuthPanel'
import './Auth.css'

interface AuthDialogProps {
  open: boolean
  onClose: () => void
}

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * The header's "Zaloguj się": the same panel as in the wizard, in a modal dialog (a bottom sheet on phones).
 * Native <dialog> makes the page inert; Tab is also kept inside explicitly, Escape closes, focus returns to the trigger.
 */
export function AuthDialog({ open, onClose }: AuthDialogProps) {
  if (!open) return null
  return <OpenDialog onClose={onClose} />
}

function OpenDialog({ onClose }: { onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
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
    // showModal() focuses the first control (the close button); the e-mail field is what the user came for.
    dialog.querySelector<HTMLElement>('input')?.focus()

    // Escape on a modal dialog fires "cancel"; React owns the open state, so close through it.
    const onCancel = (event: Event) => {
      event.preventDefault()
      onCloseRef.current()
    }
    dialog.addEventListener('cancel', onCancel)
    return () => {
      dialog.removeEventListener('cancel', onCancel)
      if (dialog.open && typeof dialog.close === 'function') dialog.close()
      returnTo?.focus()
    }
  }, [])

  const onKeyDown = (event: KeyboardEvent<HTMLDialogElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      onClose()
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
      className="auth-dialog"
      aria-labelledby={titleId}
      aria-modal="true"
      onKeyDown={onKeyDown}
      // A press on the backdrop (the dialog box itself, outside its content) closes it.
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div className="auth-dialog__content">
        <button type="button" className="auth-dialog__close" aria-label="Zamknij" onClick={onClose}>
          <CloseIcon size={20} />
        </button>
        <AuthPanel
          as="div"
          title="Zaloguj się"
          titleId={titleId}
          description="Na koncie zapiszesz sprawy i pisma, a dane do reklamacji nie będą wpisywane od nowa."
          focus="email"
          onSuccess={onClose}
        />
      </div>
    </dialog>
  )
}
