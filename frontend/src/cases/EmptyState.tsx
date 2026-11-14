import { Link } from 'react-router'
import { ArrowRightIcon, TicketIcon } from '../components/icons'

/** No cases yet: say what this page will hold and start a check. */
export function EmptyState() {
  return (
    <div className="cases-empty">
      <span className="cases-empty__icon">
        <TicketIcon size={24} />
      </span>
      <h2 className="cases-empty__title">Nie masz jeszcze zapisanych spraw</h2>
      <p className="cases-empty__text">
        Sprawdź bilet z opóźnionego pociągu. Jeśli należy Ci się odszkodowanie, zapiszesz sprawę i przygotujesz pismo – tu
        będziesz śledzić jej status i termin odpowiedzi przewoźnika.
      </p>
      <Link to="/sprawdz" className="button button--primary button--md">
        <span className="button__content">
          Sprawdź bilet
          <ArrowRightIcon size={18} />
        </span>
      </Link>
    </div>
  )
}
