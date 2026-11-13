import type { ChangeEvent } from 'react'
import { filterAllowed, finalizeText, type TextField } from '../lib/textRules'

type TextControl = HTMLInputElement | HTMLTextAreaElement

/**
 * Keeps disallowed characters (emoji, symbols) out of a text control: every change — typing, paste, autofill,
 * dictation — is canonicalized and filtered before it reaches state, and the caret stays where the user was.
 * Trims on blur, never while typing. Without a field the value passes through untouched.
 */
export function useAllowedInput<E extends TextControl>(
  field: TextField | undefined,
  value: string,
  onValueChange: (value: string) => void,
) {
  const onChange = (event: ChangeEvent<E>) => {
    const element = event.target
    const raw = element.value
    if (!field) {
      onValueChange(raw)
      return
    }
    const next = filterAllowed(field, raw)
    if (next !== raw) {
      // Write the filtered text back right away so a rejected character never paints, and keep the caret in place.
      const caret = Math.min(filterAllowed(field, raw.slice(0, element.selectionStart ?? raw.length)).length, next.length)
      element.value = next
      if (document.activeElement === element) element.setSelectionRange(caret, caret)
    }
    onValueChange(next)
  }

  const onBlur = () => {
    if (!field) return
    const finalized = finalizeText(field, value)
    if (finalized !== value) onValueChange(finalized)
  }

  return { onChange, onBlur }
}
