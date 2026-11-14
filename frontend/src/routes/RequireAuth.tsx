import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { signInPath } from './paths'

/** Protected pages: wait for the session check, then send guests to sign-in with the way back. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth()
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
  if (!user) return <Navigate to={signInPath(location.pathname + location.search)} replace />
  return children
}
