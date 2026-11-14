import { useId, type ReactNode } from 'react'
import { CheckIcon } from './icons'
import './Checkbox.css'

interface CheckboxProps {
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  label: string
  /** Secondary line under the label (announced as the description, not as part of the name). */
  description?: ReactNode
  /** "switch" for an on/off setting that changes the form right away (e.g. hides a field). */
  variant?: 'checkbox' | 'switch'
  disabled?: boolean
}

/** A native checkbox with a 44 px tap row: the whole label is the target. */
export function Checkbox({ checked, onCheckedChange, label, description, variant = 'checkbox', disabled }: CheckboxProps) {
  const id = useId()
  const labelId = `${id}-label`
  const descriptionId = description ? `${id}-description` : undefined

  return (
    <label className={`check check--${variant}`} data-disabled={disabled || undefined}>
      <input
        type="checkbox"
        role={variant === 'switch' ? 'switch' : undefined}
        className="check__input"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onCheckedChange(event.target.checked)}
        aria-labelledby={labelId}
        aria-describedby={descriptionId}
      />
      <span className="check__control" aria-hidden="true">
        {variant === 'checkbox' ? <CheckIcon size={16} strokeWidth={2.5} /> : <span className="check__thumb" />}
      </span>
      <span className="check__text">
        <span className="check__label" id={labelId}>
          {label}
        </span>
        {description && (
          <span className="check__description" id={descriptionId}>
            {description}
          </span>
        )}
      </span>
    </label>
  )
}
