import { useState } from 'react'
import { AccountMenu } from './components/AccountMenu'
import { AppShell } from './components/AppShell'
import { Badge } from './components/Badge'
import { Button } from './components/Button'
import { Card } from './components/Card'
import { ArrowRightIcon, CheckCircleIcon, ClockIcon, TicketIcon } from './components/icons'
import { verdictStyle } from './lib/verdict'
import { Wizard } from './wizard/Wizard'
import './App.css'

const steps = [
  { icon: <TicketIcon />, title: 'Dodaj bilet', text: 'Zdjęcie lub PDF – dane odczytamy za Ciebie albo wpiszesz je sam.' },
  { icon: <ClockIcon />, title: 'Podaj opóźnienie', text: '60–119 min to 25% ceny biletu, od 120 min – 50%.' },
  { icon: <CheckCircleIcon />, title: 'Wyślij reklamację', text: 'Gotowe pismo do przewoźnika, w PDF, w minutę.' },
]

type View = 'home' | 'wizard'

export default function App() {
  // No router yet (Task 11): a local switch between the home page and the wizard.
  const [view, setView] = useState<View>('home')

  const show = (next: View) => {
    setView(next)
    window.scrollTo({ top: 0 })
  }
  // Sign-in arrives with the inline auth panel (Task 10) and account pages (Task 11).
  const signIn = () => {}
  const scrollToSteps = () => document.getElementById('how')?.scrollIntoView({ behavior: 'smooth', block: 'start' })

  if (view === 'wizard') {
    return (
      <AppShell account={<AccountMenu onSignIn={signIn} />} onHome={() => show('home')}>
        <Wizard />
      </AppShell>
    )
  }

  return (
    <AppShell account={<AccountMenu onSignIn={signIn} />} onHome={() => show('home')}>
      <section className="hero">
        <div className="container hero__inner">
          <Badge tone="accent">Pociągi dalekobieżne w Polsce</Badge>
          <h1 className="hero__title">Odzyskaj pieniądze za opóźniony pociąg</h1>
          <p className="hero__lead">
            Sprawdź w minutę, ile Ci się należy, i wyślij gotową reklamację. Bez prawniczego języka i bez zgadywania.
          </p>
          <div className="hero__actions">
            <Button iconEnd={<ArrowRightIcon size={18} />} onClick={() => show('wizard')}>
              Sprawdź swój bilet
            </Button>
            <Button variant="ghost" onClick={scrollToSteps}>
              Jak to działa?
            </Button>
          </div>
        </div>
      </section>

      <section id="how" className="container steps" aria-label="Jak to działa">
        {steps.map((step, index) => (
          <Card key={step.title} className="step">
            <span className="step__icon">{step.icon}</span>
            <div>
              <h2 className="step__title">
                <span className="step__number tabular">{index + 1}.</span> {step.title}
              </h2>
              <p className="step__text">{step.text}</p>
            </div>
          </Card>
        ))}
      </section>

      <section id="preview" className="container preview" aria-labelledby="preview-title">
        <h2 id="preview-title" className="section-title">
          Tak wygląda wynik
        </h2>

        <Card elevation="raised" padding="lg" className="result">
          <div className="result__head">
            <Badge tone={verdictStyle.Eligible.tone} icon={<CheckCircleIcon />}>
              {verdictStyle.Eligible.label}
            </Badge>
            <span className="result__meta">IC 5310 · Kraków Główny → Warszawa Centralna</span>
          </div>
          <p className="result__amount tabular">
            42,50<span className="result__currency"> zł</span>
          </p>
          <p className="result__reason">Opóźnienie 75 min – przysługuje 25% ceny biletu (170,00 zł).</p>
          <div className="result__verdicts" aria-label="Możliwe werdykty">
            {Object.values(verdictStyle).map((verdict) => (
              <Badge key={verdict.label} tone={verdict.tone}>
                {verdict.label}
              </Badge>
            ))}
          </div>
        </Card>

      </section>
    </AppShell>
  )
}
