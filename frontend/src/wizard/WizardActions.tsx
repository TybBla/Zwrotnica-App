import { useId, type ReactNode } from 'react'
import { Button } from '../components/Button'
import { ArrowLeftIcon } from '../components/icons'

interface WizardActionsProps {
  onBack?: () => void
  /** Why the primary action is not available yet; linked to it via `hintId`. */
  hint?: ReactNode
  /** The primary action; receives the hint's id for aria-describedby. */
  children: (hintId: string | undefined) => ReactNode
}

/** Back + primary action. On phones it sticks to the bottom of the screen on the same glass as the header. */
export function WizardActions({ onBack, hint, children }: WizardActionsProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  return (
    <div className="wizard-actions">
      {hint && (
        <p className="wizard-actions__hint" id={hintId}>
          {hint}
        </p>
      )}
      <div className="wizard-actions__row">
        {onBack && (
          <Button variant="secondary" iconStart={<ArrowLeftIcon size={18} />} onClick={onBack} className="wizard-actions__back">
            Wstecz
          </Button>
        )}
        {children(hintId)}
      </div>
    </div>
  )
}
