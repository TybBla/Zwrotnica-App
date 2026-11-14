import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { signInPath } from './paths'

/**
 * Protected pages: wait for the session check, then send guests to sign-in with the way back.
 * When the check could not reach the server, the user is not sent to sign-in (they may well be signed in):
 * the page says why it cannot show anything yet, and the notice under the header offers "Spróbuj ponownie".
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, loading, offline } = useAuth()
  const location = useLocation()

  if (loading) {
    return (
      <div className="container page" aria-busy="true">
        <p className="page__loading" role="status">
          Sprawdzamy, czy jesteś zalogowany…
        </p>
      </div>
    )
  }
  if (!user && offline) {
    return (
      <div className="container page">
        <p className="page__loading">Ta strona wymaga połączenia z serwerem. Spróbuj ponownie za chwilę.</p>
      </div>
    )
  }
  if (!user) return <Navigate to={signInPath(location.pathname + location.search)} replace />
  return children
}
