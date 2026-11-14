import { ExternalLinkIcon } from '../components/icons'
import { sources } from './content'
import './SiteFooter.css'

/** Legal note, sources and the project's status – on every page. */
export function SiteFooter() {
  return (
    <div className="container container--wide site-footer">
      <div className="site-footer__about">
        <p className="site-footer__name">Refund Pilot</p>
        <p>
          <strong>Narzędzie pomocnicze – nie stanowi porady prawnej.</strong> Przed wysłaniem reklamacji sprawdź regulamin
          swojego przewoźnika.
        </p>
        <p>Projekt rekrutacyjny. Nie jest usługą PKP Intercity ani żadnego innego przewoźnika.</p>
      </div>
      <nav className="site-footer__sources" aria-labelledby="footer-sources-title">
        <h2 className="site-footer__title" id="footer-sources-title">
          Źródła
        </h2>
        <ul>
          {sources.map((source) => (
            <li key={source.href}>
              <a href={source.href} target="_blank" rel="noreferrer">
                {source.label}
                <ExternalLinkIcon size={14} />
                <span className="visually-hidden"> (otwiera się w nowej karcie)</span>
              </a>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
