import { useRef, type ChangeEvent, type CompositionEvent } from 'react'
import { filterAllowed, finalizeText, type TextField } from '../lib/textRules'

type TextControl = HTMLInputElement | HTMLTextAreaElement

/**
 * Keeps disallowed characters (emoji, symbols) out of a text control: every change — typing, paste, autofill,
 * dictation — is canonicalized and filtered before it reaches state, and the caret stays where the user was.
 * While an IME or dead-key composition is in progress (e.g. Option+E then o → ó) the raw text passes through
 * untouched — rewriting the value mid-composition would abort it — and is filtered once the composition ends.
 * Trims on blur, never while typing. Without a field the value passes through untouched.
 */
export function useAllowedInput<E extends TextControl>(
  field: TextField | undefined,
  value: string,
  onValueChange: (value: string) => void,
) {
  const composing = useRef(false)

  const applyFilter = (element: E) => {
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

  const onChange = (event: ChangeEvent<E>) => {
    const nativeComposing = (event.nativeEvent as Partial<InputEvent>).isComposing === true
    if (composing.current || nativeComposing) {
      onValueChange(event.target.value)
      return
    }
    applyFilter(event.target)
  }

  const onCompositionStart = () => {
    composing.current = true
  }

  const onCompositionEnd = (event: CompositionEvent<E>) => {
    composing.current = false
    applyFilter(event.currentTarget)
  }

  const onBlur = () => {
    composing.current = false
    if (!field) return
    const finalized = finalizeText(field, value)
    if (finalized !== value) onValueChange(finalized)
  }

  return { onChange, onCompositionStart, onCompositionEnd, onBlur }
}
