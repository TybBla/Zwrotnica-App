import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError, api } from '../api/client'
import type { CaseDto, CaseStatus, ComplaintDto } from '../api/types'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { AlertCircleIcon, ArrowLeftIcon, FileIcon, TrashIcon } from '../components/icons'
import { statusLabels } from '../hooks/useCases'
import { warsawDate } from '../lib/deadline'
import { formatDate } from '../lib/format'
import { verdictStyle } from '../lib/verdict'
import { usePageTitle } from '../routes/usePageTitle'
import { LetterChoice } from '../wizard/LetterChoice'
import { caseLetterKind, caseRoute, caseTitle, caseVerdict, causeLabels, formatPln, incidentText, payoutLabels } from './caseText'
import { DeadlineBadge } from './DeadlineBadge'
import { StatusSelect } from './StatusSelect'
import './Cases.css'
import '../wizard/Result.css'

type Load = { state: 'loading' } | { state: 'error'; message: string; notFound: boolean } | { state: 'ready'; item: CaseDto }

/** A stored instant (UTC) as the Warsaw calendar date. */
const instantDate = (iso: string) => formatDate(warsawDate(new Date(iso)))

/** /sprawy/:id – the case's data, its status and deadline, and the letter again (generated anew from the saved case). */
export function CaseDetailPage() {
  const { id: rawId } = useParams()
  const id = Number(rawId)
  // key: another case starts from a clean state (no letter or error carried over).
  return <CaseDetail key={id} id={Number.isInteger(id) && id > 0 ? id : null} />
}

const notFoundMessage = 'Nie ma takiej sprawy – mogła zostać usunięta.'

function CaseDetail({ id }: { id: number | null }) {
  const navigate = useNavigate()
  const [load, setLoad] = useState<Load>(
    id === null ? { state: 'error', message: notFoundMessage, notFound: true } : { state: 'loading' },
  )
  const [statusError, setStatusError] = useState<string | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [letter, setLetter] = useState<ComplaintDto | null>(null)
  const [letterBusy, setLetterBusy] = useState(false)
  const [letterError, setLetterError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  usePageTitle(load.state === 'ready' ? caseTitle(load.item) : 'Sprawa')

  useEffect(() => {
    if (id === null) return
    let active = true
    api
      .getCase(id)
      .then((item) => active && setLoad({ state: 'ready', item }))
      .catch((error: unknown) => {
        if (!active) return
        const notFound = error instanceof ApiError && error.status === 404
        setLoad({
          state: 'error',
          notFound,
          message: notFound ? notFoundMessage : error instanceof ApiError ? error.detail : 'Nie udało się wczytać sprawy.',
        })
      })
    return () => {
      active = false
    }
  }, [id, attempt])

  const letterAbort = useRef<AbortController | null>(null)
  useEffect(() => () => letterAbort.current?.abort(), [])

  const changeStatus = async (item: CaseDto, status: CaseStatus) => {
    if (status === item.status) return
    setStatusError(null)
    setLoad({ state: 'ready', item: { ...item, status } })
    try {
      const saved = await api.setStatus(item.id, status)
      setLoad({ state: 'ready', item: saved })
      setAnnouncement(`Status zmieniony: ${statusLabels[status]}.`)
    } catch (error) {
      setLoad((current) =>
        current.state === 'ready' && current.item.status === status ? { state: 'ready', item: { ...current.item, status: item.status } } : current,
      )
      const reason = error instanceof ApiError ? ` ${error.detail}` : ''
      setStatusError(`Nie udało się zmienić statusu – przywrócono „${statusLabels[item.status]}”.${reason}`)
    }
  }

  const prepareLetter = async (item: CaseDto) => {
    letterAbort.current?.abort()
    const controller = new AbortController()
    letterAbort.current = controller
    setLetterBusy(true)
    setLetterError(null)
    setLetter(null)
    try {
      // The account number (for a transfer) comes from the profile; none is typed here.
      const result = await api.generateComplaint(
        item.id,
        { payout: item.payoutPreference, oneTimeIban: null, saveIbanToProfile: false },
        controller.signal,
      )
      setLetter(result)
    } catch (error) {
      if (controller.signal.aborted) return
      setLetterError(
        error instanceof ApiError && error.status === 409
          ? 'Dla tej sprawy nie należy się odszkodowanie, więc pisma nie przygotujemy.'
          : error instanceof ApiError
            ? error.detail
            : 'Nie udało się przygotować pisma. Spróbuj ponownie.',
      )
    } finally {
      if (!controller.signal.aborted) setLetterBusy(false)
    }
  }

  const heading =
    load.state === 'ready' ? caseTitle(load.item) : load.state === 'error' && load.notFound ? 'Nie znaleziono sprawy' : 'Sprawa'

  return (
    <div className="container container--wide page page--column case-detail">
      <Link to="/sprawy" className="page__back">
        <ArrowLeftIcon size={18} />
        Moje sprawy
      </Link>

      {/* One h1 element in every state: the route focus lands on it while loading and stays when the data arrives. */}
      <header className="page__head">
        {/* A typed-in carrier can be 200 characters: shown in full here, one size smaller (7 → 4 lines at 375 px). */}
        <h1 className="page__title" tabIndex={-1} data-long={heading.length > 48 || undefined}>
          {heading}
        </h1>
        {load.state === 'ready' && <CaseLead item={load.item} />}
      </header>

      {load.state === 'loading' && (
        <p className="page__loading" role="status">
          Wczytywanie sprawy…
        </p>
      )}

      {load.state === 'error' && (
        <>
          <div className="page-error" role="alert" data-kind={load.notFound ? 'final' : undefined}>
            <AlertCircleIcon className="page-error__icon" />
            <div className="page-error__body">
              <p>{load.message}</p>
              {load.notFound ? (
                <Link to="/sprawy">Wróć do listy spraw</Link>
              ) : (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setLoad({ state: 'loading' })
                    setAttempt((value) => value + 1)
                  }}
                >
                  Spróbuj ponownie
                </Button>
              )}
            </div>
          </div>
        </>
      )}

      {load.state === 'ready' && <Details item={load.item} />}

      {load.state === 'ready' && (
        <>
          <section className="case-detail__section" aria-labelledby="case-status-title">
            <h2 className="case-detail__subtitle" id="case-status-title">
              Status
            </h2>
            <div className="case-detail__status">
              <StatusSelect value={load.item.status} onChange={(status) => void changeStatus(load.item, status)} />
              <DeadlineBadge status={load.item.status} responseDueDate={load.item.responseDueDate} />
            </div>
            <p className="case-detail__hint">
              Ustaw „Wysłana”, gdy wyślesz pismo – policzymy miesiąc na odpowiedź przewoźnika.
            </p>
            {statusError && (
              <p className="case-row__error" role="alert">
                <AlertCircleIcon size={16} />
                {statusError}
              </p>
            )}
            <p className="visually-hidden" role="status" aria-live="polite">
              {announcement}
            </p>
          </section>

          {caseLetterKind(load.item) !== null && (
            <section className="case-detail__section" aria-labelledby="case-letter-title">
              <h2 className="case-detail__subtitle" id="case-letter-title">
                Pismo do przewoźnika
              </h2>
              {!letter && (
                <>
                  <p className="case-detail__hint">
                    {caseLetterKind(load.item) === 'carrierRules'
                      ? 'Przygotujemy reklamację od nowa – przewoźnik rozpatrzy ją według swojego regulaminu, bez gwarantowanej kwoty. Użyjemy'
                      : 'Przygotujemy pismo od nowa z'}{' '}
                    danych tej sprawy i Twojego profilu (forma wypłaty: {payoutLabels[load.item.payoutPreference].toLowerCase()}).
                  </p>
                  <div>
                    <Button
                      variant="primary"
                      iconStart={<FileIcon size={18} />}
                      loading={letterBusy}
                      onClick={() => void prepareLetter(load.item)}
                    >
                      {caseLetterKind(load.item) === 'carrierRules' ? 'Przygotuj reklamację ponownie' : 'Przygotuj pismo ponownie'}
                    </Button>
                  </div>
                  {letterBusy && (
                    <p className="result__progress" role="status">
                      <span className="result__spinner" aria-hidden="true" />
                      Przygotowujemy pismo – to może potrwać do minuty.
                    </p>
                  )}
                </>
              )}
              {letterError && (
                <div className="page-error" role="alert">
                  <AlertCircleIcon className="page-error__icon" />
                  <div className="page-error__body">
                    <p>{letterError}</p>
                  </div>
                </div>
              )}
              {letter && <LetterChoice caseId={load.item.id} letter={letter} />}
            </section>
          )}

          <section className="case-detail__section case-detail__danger" aria-labelledby="case-delete-title">
            <h2 className="case-detail__subtitle" id="case-delete-title">
              Usuń sprawę
            </h2>
            <p className="case-detail__hint">Sprawa i jej dane znikną z konta. Wysłanego pisma to nie wycofa.</p>
            <div>
              <Button variant="secondary" iconStart={<TrashIcon size={18} />} onClick={() => setConfirmDelete(true)}>
                Usuń sprawę
              </Button>
            </div>
          </section>

          <ConfirmDialog
            open={confirmDelete}
            title="Usunąć sprawę?"
            confirmLabel="Usuń sprawę"
            onConfirm={async () => {
              try {
                await api.deleteCase(load.item.id)
              } catch (error) {
                if (!(error instanceof ApiError && error.status === 404)) throw error
              }
              navigate('/sprawy', { replace: true })
            }}
            onClose={() => setConfirmDelete(false)}
          >
            <p>
              {caseTitle(load.item)}, przejazd {formatDate(load.item.travelDate)}. Tego nie można cofnąć.
            </p>
          </ConfirmDialog>
        </>
      )}
    </div>
  )
}

function CaseLead({ item }: { item: CaseDto }) {
  const verdict = verdictStyle[caseVerdict(item)]
  return (
    <p className="case-detail__lead">
      <Badge tone={verdict.tone}>{verdict.label}</Badge>
      <span className="case-detail__amount mono tabular">{formatPln(item.compensationAmount)}</span>
    </p>
  )
}

function Details({ item }: { item: CaseDto }) {
  const route = caseRoute(item)
  return (
    <>
      <dl className="case-facts">
        <Fact term="Trasa">{route ?? <span className="case-row__unknown">trasa nieznana</span>}</Fact>
        <Fact term="Data przejazdu">{formatDate(item.travelDate)}</Fact>
        <Fact term="Zdarzenie">{incidentText(item)}</Fact>
        {item.delayCause && <Fact term="Przyczyna">{causeLabels[item.delayCause]}</Fact>}
        <Fact term="Cena biletu">
          <span className="mono tabular">{formatPln(item.ticketPrice)}</span>
          {item.isSeasonTicket && ' (bilet okresowy)'}
        </Fact>
        {item.ticketNumber && (
          <Fact term="Numer biletu">
            <span className="mono">{item.ticketNumber}</span>
          </Fact>
        )}
        <Fact term="Forma wypłaty">{payoutLabels[item.payoutPreference]}</Fact>
        <Fact term="Zapisano">{instantDate(item.createdAt)}</Fact>
        {item.filedAt && <Fact term="Wysłano">{instantDate(item.filedAt)}</Fact>}
      </dl>
      {item.refusalRisk && (
        <p className="case-detail__note" role="note">
          Przyczyna opóźnienia może zostać uznana za siłę wyższą – przewoźnik może odmówić wypłaty.
        </p>
      )}
    </>
  )
}

function Fact({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div className="case-facts__row">
      <dt className="case-facts__term">{term}</dt>
      <dd className="case-facts__value">{children}</dd>
    </div>
  )
}
