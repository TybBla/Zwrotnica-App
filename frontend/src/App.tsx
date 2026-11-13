import { useState } from 'react'
import type { PayoutPreference } from './api/types'
import { AccountMenu } from './components/AccountMenu'
import { AppShell } from './components/AppShell'
import { Badge } from './components/Badge'
import { Button } from './components/Button'
import { Card } from './components/Card'
import { Field } from './components/Field'
import {
  AlertCircleIcon,
  ArrowRightIcon,
  CheckCircleIcon,
  ClockIcon,
  ShieldIcon,
  TicketIcon,
} from './components/icons'
import { Select, type SelectOption } from './components/Select'
import { TextInput } from './components/TextInput'
import { verdictStyle } from './lib/verdict'
import './App.css'

const payoutOptions: SelectOption<PayoutPreference>[] = [
  { value: 'OriginalPayment', label: 'Zwrot na kartę lub konto, z którego płaciłeś' },
  { value: 'BankTransfer', label: 'Przelew na inne konto' },
  { value: 'Voucher', label: 'Bon na kolejną podróż' },
]

const steps = [
  { icon: <TicketIcon />, title: 'Dodaj bilet', text: 'Zdjęcie lub PDF – dane odczytamy za Ciebie albo wpiszesz je sam.' },
  { icon: <ClockIcon />, title: 'Podaj opóźnienie', text: '60–119 min to 25% ceny biletu, od 120 min – 50%.' },
  { icon: <CheckCircleIcon />, title: 'Wyślij reklamację', text: 'Gotowe pismo do przewoźnika, w PDF, w minutę.' },
]

export default function App() {
  const [station, setStation] = useState('')
  const [payout, setPayout] = useState<PayoutPreference | ''>('')
  const [checking, setChecking] = useState(false)

  const scrollToPreview = () => document.getElementById('preview')?.scrollIntoView({ behavior: 'smooth', block: 'start' })

  return (
    <AppShell account={<AccountMenu onSignIn={scrollToPreview} />}>
      <section className="hero">
        <div className="container hero__inner">
          <Badge tone="accent">Pociągi dalekobieżne w Polsce</Badge>
          <h1 className="hero__title">Odzyskaj pieniądze za opóźniony pociąg</h1>
          <p className="hero__lead">
            Sprawdź w minutę, ile Ci się należy, i wyślij gotową reklamację. Bez prawniczego języka i bez zgadywania.
          </p>
          <div className="hero__actions">
            <Button iconEnd={<ArrowRightIcon size={18} />} onClick={scrollToPreview}>
              Sprawdź swój bilet
            </Button>
            <Button variant="ghost" onClick={scrollToPreview}>
              Jak to działa?
            </Button>
          </div>
        </div>
      </section>

      <section className="container steps" aria-label="Jak to działa">
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

        <Card padding="lg" className="form-preview">
          <form
            className="stack"
            onSubmit={(event) => {
              event.preventDefault()
              setChecking(true)
              window.setTimeout(() => setChecking(false), 900)
            }}
          >
            <Field
              label="Stacja początkowa"
              optional
              hint="Litery, cyfry i znaki . - ' ( ) /. Emoji się nie wpiszą."
              tooltip="Nazwa z biletu, np. „Kraków Główny”. Nie wpływa na kwotę – pomaga przewoźnikowi znaleźć Twój przejazd."
            >
              <TextInput field="station" value={station} onValueChange={setStation} placeholder="np. Kraków Główny" autoComplete="off" />
            </Field>
            <Field
              label="Forma wypłaty"
              tooltip="Bilet kupiony gotówką w kasie? Wybierz przelew na konto."
            >
              <Select value={payout} onValueChange={setPayout} options={payoutOptions} placeholder="Wybierz formę wypłaty" />
            </Field>
            <p className="form-preview__note">
              <ShieldIcon size={18} />
              Numer konta jest szyfrowany, widoczny tylko dla Ciebie i nigdy nie trafia do AI.
            </p>
            <div className="form-preview__actions">
              <Button type="submit" loading={checking} block>
                Sprawdź, ile mi się należy
              </Button>
              <Button variant="secondary" block onClick={() => setStation('')}>
                Wyczyść
              </Button>
            </div>
            <p className="form-preview__hint">
              <AlertCircleIcon size={16} />
              Podgląd komponentów – pełny formularz pojawi się w kreatorze.
            </p>
          </form>
        </Card>
      </section>
    </AppShell>
  )
}
