import { useId, useMemo, type ReactNode } from 'react'
import { AlertCircleIcon, InfoIcon } from './icons'
import { FieldContext } from './fieldContext'
import { Tooltip } from './Tooltip'
import './Field.css'

interface FieldProps {
  label: string
  hint?: ReactNode
  /** Information about the chosen value (not an error, never blocks), shown as a quiet box under the control. */
  note?: ReactNode
  /** Shown instead of nothing below the control; marks the control aria-invalid. */
  error?: string | null
  /** Announce the note politely when it appears (a live region that stays mounted, so the change is read out). */
  liveNote?: boolean
  /** Optional ⓘ next to the label. */
  tooltip?: ReactNode
  /** Adds a quiet "opcjonalne" marker; most fields in this app are optional. */
  optional?: boolean
  required?: boolean
  /** Draws the eye to a field the user still has to fill (e.g. AI could not read it). */
  attention?: boolean
  id?: string
  children: ReactNode
}

/** Label + control + hint + error. The child control (TextInput, TextArea, Select) picks up id and ARIA wiring. */
export function Field({ label, hint, note, liveNote = false, error, tooltip, optional, required = false, attention = false, id, children }: FieldProps) {
  const generatedId = useId()
  const controlId = id ?? generatedId
  const hintId = hint ? `${controlId}-hint` : undefined
  const errorId = error ? `${controlId}-error` : undefined
  const noteId = note ? `${controlId}-note` : undefined

  const control = useMemo(
    () => ({
      id: controlId,
      describedBy: [errorId, hintId, noteId].filter(Boolean).join(' ') || undefined,
      invalid: Boolean(error),
      required,
    }),
    [controlId, errorId, hintId, noteId, error, required],
  )

  const noteContent = note && (
    <p className="field__note" id={noteId}>
      <InfoIcon size={16} />
      <span>{note}</span>
    </p>
  )

  return (
    <div className="field" data-invalid={error ? true : undefined} data-attention={attention && !error ? true : undefined}>
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
      {liveNote ? (
        <div className="field__live" role="status">
          {noteContent}
        </div>
      ) : (
        noteContent
      )}
    </div>
  )
}
