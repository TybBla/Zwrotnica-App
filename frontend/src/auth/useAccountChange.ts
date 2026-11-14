import { useEffect, useRef } from 'react'
import { useAuth } from './useAuth'

/**
 * Calls `onChange(email)` when someone who was signed in signs out or another account signs in. Signing in from a
 * guest session is not a change (what the guest typed belongs to them). StrictMode-safe: compares with a ref.
 */
export function useAccountChange(onChange: (email: string | null) => void) {
  const email = useAuth().user?.email ?? null
  const previous = useRef(email)
  const onChangeRef = useRef(onChange)
  useEffect(() => {
    onChangeRef.current = onChange
  })
  useEffect(() => {
    const before = previous.current
    previous.current = email
    if (before !== null && before !== email) onChangeRef.current(email)
  }, [email])
}
