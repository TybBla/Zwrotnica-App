import { createContext, useContext } from 'react'

export interface FieldControl {
  id: string
  /** ids of the hint and error, for aria-describedby */
  describedBy: string | undefined
  invalid: boolean
  required: boolean
}

export const FieldContext = createContext<FieldControl | null>(null)

/** Wiring a control needs from the surrounding <Field>; null when used on its own. */
export function useFieldControl(): FieldControl | null {
  return useContext(FieldContext)
}
