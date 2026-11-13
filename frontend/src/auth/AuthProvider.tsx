import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { UserInfo } from '../api/types'
import { AuthContext, type AuthState } from './useAuth'

/** Cookie session (ASP.NET Identity): the browser holds the cookie, this only tracks who is signed in. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    api
      .me()
      .then((info) => active && setUser(info))
      .catch(() => {
        // 401 means "not signed in"; an unreachable API also leaves the user signed out — the wizard still works.
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    await api.login(email, password)
    setUser(await api.me())
  }, [])

  const register = useCallback(async (email: string, password: string) => {
    await api.register(email, password)
    await api.login(email, password)
    setUser(await api.me())
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setUser(null)
    }
  }, [])

  const value = useMemo<AuthState>(
    () => ({ user, loading, login, register, logout }),
    [user, loading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
