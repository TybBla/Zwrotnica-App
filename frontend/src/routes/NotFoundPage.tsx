import { Link } from 'react-router'
import { usePageTitle } from './usePageTitle'

/** Any unknown address. */
export function NotFoundPage() {
  usePageTitle('Nie ma takiej strony')
  return (
    <div className="container page">
      <header className="page__head">
        <h1 className="page__title" tabIndex={-1}>
          Nie ma takiej strony
        </h1>
        <p className="page__lead">Adres mógł się zmienić albo zawiera literówkę.</p>
      </header>
      <p>
        <Link to="/">Przejdź na stronę główną</Link>
      </p>
    </div>
  )
}
