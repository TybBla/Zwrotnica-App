import { useId, type ReactNode } from 'react'
import { CheckIcon, ShieldIcon } from '../components/icons'
import type { TicketSource } from '../hooks/useWizard'
import { TicketStartOptions } from '../wizard/StartChoice'
import { faq, preparation, rules } from './content'
import './Home.css'

/** The home page is the tool: it opens on the start panel, then gives the rules, what to prepare and real answers. */
export function Home({ onStart }: { onStart: (source: TicketSource) => void }) {
  return (
    <>
      <section className="home-intro" aria-labelledby="home-title">
        <div className="container container--wide home-intro__grid">
          <div className="home-intro__text">
            <h1 className="home-intro__title" id="home-title">
              Opóźniony pociąg? Sprawdź, ile Ci się należy
            </h1>
            <p className="home-intro__lead">
              Za opóźnienie pociągu dalekobieżnego od 60 minut przewoźnik zwraca 25% albo 50% ceny biletu. Policzymy kwotę
              na podstawie biletu i przygotujemy reklamację, którą wyślesz przewoźnikowi.
            </p>
          </div>

          <StartPanel onStart={onStart} />

          <ul className="home-intro__points">
            <li>
              <CheckIcon size={18} />
              Sprawdzisz bez zakładania konta
            </li>
            <li>
              <CheckIcon size={18} />
              Kwota według rozporządzenia (UE) 2021/782
            </li>
            <li>
              <CheckIcon size={18} />
              Gotowe pismo: tekst do skopiowania albo PDF
            </li>
          </ul>
        </div>
      </section>

      <HomeSection id="zasady" title="Ile możesz odzyskać" lead="Zasady z rozporządzenia (UE) 2021/782 w skrócie.">
        <RulesTable />
        <p className="home-note">
          Procent liczy się od ceny faktycznie zapłaconej, czyli po uldze. Kwot poniżej progu przewoźnik może nie wypłacić.
          W PKP Intercity próg wynosi 16 zł.
        </p>
      </HomeSection>

      <HomeSection id="co-przygotowac" title="Co przygotować" lead="Zanim zaczniesz, miej pod ręką:">
        <dl className="prep-list">
          {preparation.map((item) => (
            <div key={item.title} className="prep-list__item">
              <dt className="prep-list__title">
                {item.title}
                {item.optional && <span className="prep-list__optional">opcjonalnie</span>}
              </dt>
              <dd className="prep-list__text">{item.text}</dd>
            </div>
          ))}
        </dl>
      </HomeSection>

      <HomeSection id="pytania" title="Najczęstsze pytania">
        <div className="faq">
          {faq.map((item) => (
            <details key={item.question} className="faq__item">
              <summary className="faq__question">{item.question}</summary>
              <div className="faq__answer">{item.answer}</div>
            </details>
          ))}
        </div>
      </HomeSection>
    </>
  )
}

function StartPanel({ onStart }: { onStart: (source: TicketSource) => void }) {
  const titleId = useId()
  return (
    <section className="start-panel" aria-labelledby={titleId}>
      <div className="start-panel__head">
        <h2 className="start-panel__title" id={titleId}>
          Dodaj bilet
        </h2>
        <p className="start-panel__lead">Potrzebujemy przewoźnika, daty przejazdu i ceny biletu.</p>
      </div>
      <TicketStartOptions onChoose={onStart} />
      <p className="start-panel__note">
        <ShieldIcon size={16} />
        Zdjęć biletu nie zapisujemy. Służą tylko do odczytania danych.
      </p>
    </section>
  )
}

function HomeSection({ id, title, lead, children }: { id: string; title: string; lead?: string; children: ReactNode }) {
  return (
    <section className="home-section" id={id} aria-labelledby={`${id}-title`}>
      <div className="container container--wide home-section__grid">
        <header className="home-section__head">
          <h2 className="home-section__title" id={`${id}-title`}>
            {title}
          </h2>
          {lead && <p className="home-section__lead">{lead}</p>}
        </header>
        <div className="home-section__body">{children}</div>
      </div>
    </section>
  )
}

function RulesTable() {
  return (
    <div className="rules">
      {/* Explicit roles: on phones the rows are restyled as blocks (display: block), which makes Safari and older
          Chrome drop the native table semantics; the roles keep "row 2 of 6, Sytuacja / Co przysługuje" for readers. */}
      <table className="rules__table" role="table">
        <caption className="visually-hidden">Ile przysługuje za opóźnienie lub odwołanie pociągu</caption>
        <thead role="rowgroup">
          <tr role="row">
            <th scope="col" role="columnheader">
              Sytuacja
            </th>
            <th scope="col" role="columnheader">
              Co przysługuje
            </th>
          </tr>
        </thead>
        <tbody role="rowgroup">
          {rules.map((rule) => (
            <tr key={rule.situation} data-kind={rule.kind} role="row">
              <th scope="row" role="rowheader">
                <span className="rules__situation">{rule.situation}</span>
                {rule.detail && <span className="rules__detail">{rule.detail}</span>}
              </th>
              <td role="cell">
                <span className="rules__outcome">{rule.outcome}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Wizard side column on wide screens: the same rules, at a glance. */
export function RulesSummary() {
  return (
    <aside className="rules-summary" aria-labelledby="rules-summary-title">
      <h2 className="rules-summary__title" id="rules-summary-title">
        Zasady w skrócie
      </h2>
      <dl className="rules-summary__list">
        {rules.map((rule) => (
          <div key={rule.situation} className="rules-summary__row" data-kind={rule.kind}>
            <dt>{rule.situation}</dt>
            <dd>{rule.outcome}</dd>
          </div>
        ))}
      </dl>
      <p className="rules-summary__note">Narzędzie pomocnicze. Nie stanowi porady prawnej.</p>
    </aside>
  )
}
