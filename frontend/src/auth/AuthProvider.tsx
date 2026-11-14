import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { ApiError, api } from '../api/client'
import type { UserInfo } from '../api/types'
import { AuthContext, type AuthState } from './useAuth'

/** 401/403 mean "no session"; anything else (no answer, 5xx) means we could not find out. */
const isSignedOutAnswer = (error: unknown) => error instanceof ApiError && (error.status === 401 || error.status === 403)

/** Cookie session (ASP.NET Identity): the browser holds the cookie, this only tracks who is signed in. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [offline, setOffline] = useState(false)
  const [retrying, setRetrying] = useState(false)
  const active = useRef(true)
  const checkId = useRef(0)

  // Only the newest check may set state (StrictMode runs the first one twice; a retry can overtake a slow check).
  // All state is set in the promise callbacks, i.e. after the answer arrives, never synchronously in the effect.
  const check = useCallback(() => {
    const id = ++checkId.current
    const current = () => active.current && id === checkId.current
    const done = () => {
      setLoading(false)
      setRetrying(false)
    }
    return api.me().then(
      (info) => {
        if (!current()) return
        setUser(info)
        setOffline(false)
        done()
      },
      (error: unknown) => {
        if (!current()) return
        if (isSignedOutAnswer(error)) {
          setUser(null)
          setOffline(false)
        } else {
          // Unreachable API: not a sign-out. Keep whatever we knew and say so; the wizard still works for guests.
          setOffline(true)
        }
        done()
      },
    )
  }, [])

  useEffect(() => {
    active.current = true
    void check()
    return () => {
      active.current = false
    }
  }, [check])

  const retry = useCallback(() => {
    setRetrying(true)
    void check()
  }, [check])

  // Back online (laptop woke up, train left the tunnel): check again without waiting for a click.
  useEffect(() => {
    if (!offline) return
    window.addEventListener('online', retry)
    return () => window.removeEventListener('online', retry)
  }, [offline, retry])

  const login = useCallback(async (email: string, password: string) => {
    await api.login(email, password)
    setUser(await api.me())
    setOffline(false)
  }, [])

  const register = useCallback(async (email: string, password: string) => {
    await api.register(email, password)
    await api.login(email, password)
    setUser(await api.me())
    setOffline(false)
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setUser(null)
    }
  }, [])

  const value = useMemo<AuthState>(
    () => ({ user, loading, offline, retrying, retry, login, register, logout }),
    [user, loading, offline, retrying, retry, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
