import type { FocusEvent, InputHTMLAttributes, ReactNode, Ref, TextareaHTMLAttributes } from 'react'
import { useAllowedInput } from '../hooks/useAllowedInput'
import type { TextField } from '../lib/textRules'
import { useFieldControl } from './fieldContext'
import './TextInput.css'

interface TextControlProps {
  value: string
  onValueChange: (value: string) => void
  /** Character allowlist to enforce (see lib/textRules). Omit for free text such as e-mail or password. */
  field?: TextField
  invalid?: boolean
}

type TextInputProps = TextControlProps &
  Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'defaultValue'> & {
    /** Unit or affix shown inside the control, e.g. "zł" or "min". */
    suffix?: ReactNode
    ref?: Ref<HTMLInputElement>
  }

export function TextInput({ value, onValueChange, field, invalid, suffix, className, onBlur, onCompositionStart, onCompositionEnd, id, ...props }: TextInputProps) {
  const control = useFieldControl()
  const allowed = useAllowedInput<HTMLInputElement>(field, value, onValueChange)
  const isInvalid = invalid ?? control?.invalid ?? false

  return (
    <div className={['input', suffix ? 'input--with-suffix' : null, className].filter(Boolean).join(' ')}>
      <input
        type="text"
        {...props}
        id={id ?? control?.id}
        value={value}
        onChange={allowed.onChange}
        onCompositionStart={(event) => {
          allowed.onCompositionStart()
          onCompositionStart?.(event)
        }}
        onCompositionEnd={(event) => {
          allowed.onCompositionEnd(event)
          onCompositionEnd?.(event)
        }}
        onBlur={(event: FocusEvent<HTMLInputElement>) => {
          allowed.onBlur()
          onBlur?.(event)
        }}
        aria-invalid={isInvalid || undefined}
        aria-describedby={[props['aria-describedby'], control?.describedBy].filter(Boolean).join(' ') || undefined}
        required={props.required ?? control?.required}
        className="input__control"
      />
      {suffix && (
        <span className="input__suffix" aria-hidden="true">
          {suffix}
        </span>
      )}
    </div>
  )
}

type TextAreaProps = TextControlProps &
  Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange' | 'defaultValue'> & {
    ref?: Ref<HTMLTextAreaElement>
  }

export function TextArea({ value, onValueChange, field, invalid, className, onBlur, onCompositionStart, onCompositionEnd, id, rows = 4, ...props }: TextAreaProps) {
  const control = useFieldControl()
  const allowed = useAllowedInput<HTMLTextAreaElement>(field, value, onValueChange)
  const isInvalid = invalid ?? control?.invalid ?? false

  return (
    <textarea
      rows={rows}
      {...props}
      id={id ?? control?.id}
      value={value}
      onChange={allowed.onChange}
      onCompositionStart={(event) => {
        allowed.onCompositionStart()
        onCompositionStart?.(event)
      }}
      onCompositionEnd={(event) => {
        allowed.onCompositionEnd(event)
        onCompositionEnd?.(event)
      }}
      onBlur={(event) => {
        allowed.onBlur()
        onBlur?.(event)
      }}
      aria-invalid={isInvalid || undefined}
      aria-describedby={[props['aria-describedby'], control?.describedBy].filter(Boolean).join(' ') || undefined}
      required={props.required ?? control?.required}
      className={['input__control', 'input__control--multiline', className].filter(Boolean).join(' ')}
    />
  )
}
