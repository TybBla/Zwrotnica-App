import { createContext, useContext } from 'react'
import type { UserInfo } from '../api/types'

export interface AuthState {
  /** null when signed out (or while the first check is running — see `loading`). */
  user: UserInfo | null
  /** True only during the initial session check on page load. */
  loading: boolean
  /**
   * The session check could not reach the API (network error or 5xx) — not the same as signed out: nobody was
   * logged out, we just do not know yet. Cleared by a successful `retry()`.
   */
  offline: boolean
  /** True while `retry()` is running. */
  retrying: boolean
  /** Repeats the session check (the "Spróbuj ponownie" button, and automatically when the browser comes back online). */
  retry: () => void
  /** Throw ApiError with a Polish `detail` on failure. */
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const auth = useContext(AuthContext)
  if (auth === null) throw new Error('useAuth must be used inside <AuthProvider>.')
  return auth
}
