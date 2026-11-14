import { useLayoutEffect, useRef, useState } from 'react'
import { Route, Routes, useLocation, useNavigate, useSearchParams } from 'react-router'
import { AuthDialog } from './auth/AuthDialog'
import { ConnectionNotice } from './auth/ConnectionNotice'
import { LoginPage } from './auth/LoginPage'
import { RegisterPage } from './auth/RegisterPage'
import { CaseDetailPage } from './cases/CaseDetailPage'
import { CasesPage } from './cases/CasesPage'
import { AccountMenu } from './components/AccountMenu'
import { AppShell } from './components/AppShell'
import { TicketIcon, UserIcon } from './components/icons'
import type { TicketSource } from './hooks/useWizard'
import { Home, RulesSummary } from './home/Home'
import { SiteFooter } from './home/SiteFooter'
import { ProfilePage } from './profile/ProfilePage'
import { NotFoundPage } from './routes/NotFoundPage'
import { RequireAuth } from './routes/RequireAuth'
import { usePageTitle } from './routes/usePageTitle'
import { Wizard } from './wizard/Wizard'
import './App.css'

/** `/sprawdz?bilet=wgraj|reczne` – the start chosen on the home page (Polish, readable in the address bar). */
const sourceParam: Record<TicketSource, string> = { upload: 'wgraj', manual: 'reczne' }
const sourceFromParam = (value: string | null): TicketSource | undefined =>
  value === sourceParam.upload ? 'upload' : value === sourceParam.manual ? 'manual' : undefined

const wizardPath = (source: TicketSource) => `/sprawdz?bilet=${sourceParam[source]}`

const PROTECTED = ['/sprawy', '/konto']

export default function App() {
  const [signInOpen, setSignInOpen] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()
  useRouteFocus(location.pathname)

  const accountItems = [
    { label: 'Moje sprawy', icon: <TicketIcon size={18} />, onSelect: () => navigate('/sprawy') },
    { label: 'Profil', icon: <UserIcon size={18} />, onSelect: () => navigate('/konto') },
  ]

  return (
    <AppShell
      account={
        <AccountMenu
          onSignIn={() => setSignInOpen(true)}
          items={accountItems}
          // Leaving a page that needs an account goes home, not to the sign-in form.
          onLogout={() => {
            if (PROTECTED.some((path) => location.pathname.startsWith(path))) navigate('/')
          }}
        />
      }
      notice={<ConnectionNotice />}
      onHome={() => navigate('/')}
      footer={<SiteFooter />}
    >
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/sprawdz" element={<WizardPage />} />
        <Route path="/logowanie" element={<LoginPage />} />
        <Route path="/rejestracja" element={<RegisterPage />} />
        <Route
          path="/sprawy"
          element={
            <RequireAuth>
              <CasesPage />
            </RequireAuth>
          }
        />
        <Route
          path="/sprawy/:id"
          element={
            <RequireAuth>
              <CaseDetailPage />
            </RequireAuth>
          }
        />
        <Route
          path="/konto"
          element={
            <RequireAuth>
              <ProfilePage />
            </RequireAuth>
          }
        />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
      <AuthDialog open={signInOpen} onClose={() => setSignInOpen(false)} />
    </AppShell>
  )
}

function HomePage() {
  usePageTitle(null)
  const navigate = useNavigate()
  return <Home onStart={(source) => navigate(wizardPath(source))} />
}

function WizardPage() {
  usePageTitle('Sprawdź bilet')
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  return (
    <div className="container container--wide wizard-layout">
      {/* Every start from the home page is a new navigation and a new wizard run (one case per run). */}
      <Wizard key={location.key} initialSource={sourceFromParam(params.get('bilet'))} onOpenCases={() => navigate('/sprawy')} />
      <RulesSummary />
    </div>
  )
}

/**
 * A client-side page change starts at the top with focus on the new page's heading (h1),
 * so screen readers announce where the user landed. The very first load keeps the browser's default.
 */
function useRouteFocus(pathname: string) {
  const first = useRef(true)
  useLayoutEffect(() => {
    if (first.current) {
      first.current = false
      return
    }
    window.scrollTo({ top: 0 })
    const main = document.getElementById('main')
    const target = main?.querySelector<HTMLElement>('h1') ?? main
    if (target && !target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1')
    target?.focus({ preventScroll: true })
  }, [pathname])
}
