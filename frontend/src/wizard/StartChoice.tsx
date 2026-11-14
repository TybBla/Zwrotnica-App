import { ArrowRightIcon, PencilIcon, ScanIcon } from '../components/icons'
import type { TicketSource } from '../hooks/useWizard'

/** The two ways in: read the ticket from a photo/PDF, or type it. Shared by the home page and the wizard's first step. */
export function TicketStartOptions({ onChoose }: { onChoose: (source: TicketSource) => void }) {
  return (
    <div className="choice-list">
      <button type="button" className="choice" data-primary onClick={() => onChoose('upload')}>
        <span className="choice__icon" aria-hidden="true">
          <ScanIcon />
        </span>
        <span className="choice__text">
          <span className="choice__title">Wgraj bilet</span>
          <span className="choice__description">Zdjęcie lub PDF – AI odczyta dane, a Ty je tylko sprawdzisz.</span>
        </span>
        <ArrowRightIcon className="choice__arrow" size={20} />
      </button>
      <button type="button" className="choice" onClick={() => onChoose('manual')}>
        <span className="choice__icon" aria-hidden="true">
          <PencilIcon />
        </span>
        <span className="choice__text">
          <span className="choice__title">Wpisz ręcznie</span>
          <span className="choice__description">Bez zdjęcia – zajmie to około minuty.</span>
        </span>
        <ArrowRightIcon className="choice__arrow" size={20} />
      </button>
    </div>
  )
}

/** The first decision: let AI read the ticket, or type it in. */
export function StartChoice({ onChoose }: { onChoose: (source: TicketSource) => void }) {
  return (
    <div className="wizard-step">
      <header className="wizard-step__head">
        <h2 className="wizard-step__title" tabIndex={-1}>
          Jak chcesz dodać bilet?
        </h2>
        <p className="wizard-step__lead">Potrzebujemy tylko kilku danych z biletu: przewoźnika, daty i ceny.</p>
      </header>
      <TicketStartOptions onChoose={onChoose} />
    </div>
  )
}
