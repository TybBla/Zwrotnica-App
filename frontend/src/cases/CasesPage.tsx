import { useRef, useState } from 'react'
import { Link } from 'react-router'
import type { CaseDto } from '../api/types'
import { Button } from '../components/Button'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { AlertCircleIcon, ArrowRightIcon } from '../components/icons'
import { useCases } from '../hooks/useCases'
import { formatDate } from '../lib/format'
import { usePageTitle } from '../routes/usePageTitle'
import { CaseRow } from './CaseRow'
import { caseTitle } from './caseText'
import { EmptyState } from './EmptyState'
import { StatsHeader } from './StatsHeader'
import './Cases.css'

/** /sprawy – the signed-in user's cases: a summary, then one row per case with status, deadline and delete. */
export function CasesPage() {
  usePageTitle('Moje sprawy')
  const { load, stats, rowErrors, announcement, reload, changeStatus, remove } = useCases()
  const [toDelete, setToDelete] = useState<CaseDto | null>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)

  return (
    <div className="container container--wide page cases-page">
      <header className="page__head cases-page__head">
        <div className="page__titles">
          <h1 className="page__title" ref={headingRef} tabIndex={-1}>
            Moje sprawy
          </h1>
          {load.state === 'ready' && load.cases.length > 0 && <StatsHeader stats={stats} />}
        </div>
        {load.state === 'ready' && load.cases.length > 0 && (
          <Link to="/sprawdz" className="button button--secondary button--sm cases-page__new">
            <span className="button__content">
              Sprawdź kolejny bilet
              <ArrowRightIcon size={18} />
            </span>
          </Link>
        )}
      </header>

      <p className="visually-hidden" role="status" aria-live="polite">
        {announcement}
      </p>

      {load.state === 'loading' && <SkeletonRows />}

      {load.state === 'error' && (
        <div className="page-error" role="alert">
          <AlertCircleIcon className="page-error__icon" />
          <div className="page-error__body">
            <p className="page-error__title">Nie udało się wczytać spraw</p>
            <p>{load.message}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
              Spróbuj ponownie
            </Button>
          </div>
        </div>
      )}

      {load.state === 'ready' && load.cases.length === 0 && <EmptyState />}

      {load.state === 'ready' && load.cases.length > 0 && (
        <ul className="case-list" aria-label="Sprawy">
          {load.cases.map((item) => (
            <CaseRow
              key={item.id}
              item={item}
              error={rowErrors[item.id]}
              onStatusChange={(status) => void changeStatus(item, status)}
              onDelete={() => setToDelete(item)}
            />
          ))}
        </ul>
      )}

      <ConfirmDialog
        open={toDelete !== null}
        title="Usunąć sprawę?"
        confirmLabel="Usuń sprawę"
        onConfirm={() => (toDelete ? remove(toDelete) : Promise.resolve())}
        onClose={() => setToDelete(null)}
        fallbackFocus={() => headingRef.current}
      >
        {toDelete && (
          <p>
            {caseTitle(toDelete)}, przejazd {formatDate(toDelete.travelDate)}. Sprawa i jej dane znikną z konta – tego nie
            można cofnąć. Wysłanego pisma to nie wycofa.
          </p>
        )}
      </ConfirmDialog>
    </div>
  )
}

function SkeletonRows() {
  return (
    <div className="case-list case-list--loading" aria-busy="true">
      <p className="visually-hidden" role="status">
        Wczytywanie spraw…
      </p>
      {[0, 1, 2].map((index) => (
        <div key={index} className="case-row case-row--skeleton" aria-hidden="true">
          <div className="case-row__main">
            <span className="skeleton skeleton--title" />
            <span className="skeleton skeleton--line" />
          </div>
          <div className="case-row__money">
            <span className="skeleton skeleton--amount" />
          </div>
        </div>
      ))}
    </div>
  )
}
