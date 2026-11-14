import { useState } from 'react'
import { AuthDialog } from './auth/AuthDialog'
import { AccountMenu } from './components/AccountMenu'
import { AppShell } from './components/AppShell'
import type { TicketSource } from './hooks/useWizard'
import { Home, RulesSummary } from './home/Home'
import { SiteFooter } from './home/SiteFooter'
import { Wizard } from './wizard/Wizard'
import './App.css'

type View = { name: 'home' } | { name: 'wizard'; source: TicketSource; run: number }

export default function App() {
  // No router yet (Task 11): a local switch between the home page and the wizard.
  const [view, setView] = useState<View>({ name: 'home' })
  const [signInOpen, setSignInOpen] = useState(false)

  const goHome = () => {
    setView({ name: 'home' })
    window.scrollTo({ top: 0 })
  }
  // Every start from the home page is a new wizard run (one case per run).
  const start = (source: TicketSource) => {
    setView({ name: 'wizard', source, run: Date.now() })
    window.scrollTo({ top: 0 })
  }

  return (
    <AppShell account={<AccountMenu onSignIn={() => setSignInOpen(true)} />} onHome={goHome} footer={<SiteFooter />}>
      {view.name === 'wizard' ? (
        <div className="container container--wide wizard-layout">
          <Wizard key={view.run} initialSource={view.source} />
          <RulesSummary />
        </div>
      ) : (
        <Home onStart={start} />
      )}
      <AuthDialog open={signInOpen} onClose={() => setSignInOpen(false)} />
    </AppShell>
  )
}
