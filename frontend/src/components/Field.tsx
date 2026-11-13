import { useId, useMemo, type ReactNode } from 'react'
import { AlertCircleIcon } from './icons'
import { FieldContext } from './fieldContext'
import { Tooltip } from './Tooltip'
import './Field.css'

interface FieldProps {
  label: string
  hint?: ReactNode
  /** Shown instead of nothing below the control; marks the control aria-invalid. */
  error?: string | null
  /** Optional ⓘ next to the label. */
  tooltip?: ReactNode
  /** Adds a quiet "opcjonalne" marker; most fields in this app are optional. */
  optional?: boolean
  required?: boolean
  id?: string
  children: ReactNode
}

/** Label + control + hint + error. The child control (TextInput, TextArea, Select) picks up id and ARIA wiring. */
export function Field({ label, hint, error, tooltip, optional, required = false, id, children }: FieldProps) {
  const generatedId = useId()
  const controlId = id ?? generatedId
  const hintId = hint ? `${controlId}-hint` : undefined
  const errorId = error ? `${controlId}-error` : undefined

  const control = useMemo(
    () => ({
      id: controlId,
      describedBy: [errorId, hintId].filter(Boolean).join(' ') || undefined,
      invalid: Boolean(error),
      required,
    }),
    [controlId, errorId, hintId, error, required],
  )

  return (
    <div className="field" data-invalid={error ? true : undefined}>
      <div className="field__label-row">
        <label className="field__label" htmlFor={controlId}>
          {label}
          {optional && (
            <>
              {' '}
              <span className="field__optional">opcjonalne</span>
            </>
          )}
        </label>
        {tooltip && <Tooltip label={`Więcej informacji: ${label}`} content={tooltip} />}
      </div>
      <FieldContext.Provider value={control}>{children}</FieldContext.Provider>
      {error && (
        <p className="field__error" id={errorId} role="alert">
          <AlertCircleIcon size={16} />
          {error}
        </p>
      )}
      {hint && (
        <p className="field__hint" id={hintId}>
          {hint}
        </p>
      )}
    </div>
  )
}
