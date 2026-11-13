import { createContext, useContext } from 'react'
import type { UserInfo } from '../api/types'

export interface AuthState {
  /** null when signed out (or while the first check is running — see `loading`). */
  user: UserInfo | null
  /** True only during the initial session check on page load. */
  loading: boolean
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
