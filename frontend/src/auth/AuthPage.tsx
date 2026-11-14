import { Navigate, useSearchParams } from 'react-router'
import { safeNext } from '../routes/paths'
import { usePageTitle } from '../routes/usePageTitle'
import { AuthPanel, type Tab } from './AuthPanel'
import { useAuth } from './useAuth'
import './Auth.css'

const copy: Record<Tab, { title: string; description: string }> = {
  login: {
    title: 'Zaloguj się',
    description: 'Na koncie są Twoje sprawy, pisma i dane do reklamacji.',
  },
  register: {
    title: 'Załóż konto',
    description: 'Konto jest potrzebne tylko do zapisania spraw. Sprawdzenie biletu działa bez niego.',
  },
}

/** /logowanie and /rejestracja: the same panel as the header dialog, as a page; afterwards back to `?next=`. */
export function AuthPage({ tab }: { tab: Tab }) {
  const { user, loading } = useAuth()
  const [params] = useSearchParams()
  const { title, description } = copy[tab]
  usePageTitle(title)

  if (user) return <Navigate to={safeNext(params.get('next'))} replace />

  return (
    <div className="container page auth-page">
      {loading ? (
        <p className="page__loading" role="status">
          Sprawdzamy, czy jesteś zalogowany…
        </p>
      ) : (
        // key: switching between /logowanie and /rejestracja opens the matching tab.
        <AuthPanel key={tab} title={title} titleAs="h1" description={description} initialTab={tab} focus="heading" />
      )}
    </div>
  )
}
