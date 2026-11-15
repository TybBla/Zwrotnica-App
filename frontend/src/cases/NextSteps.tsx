import { useEffect, useRef, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { CaseDto, ComplaintDto, FollowUpKind } from '../api/types'
import { Button } from '../components/Button'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { AlertCircleIcon, FileIcon } from '../components/icons'
import { warsawDate } from '../lib/deadline'
import { formatDate } from '../lib/format'
import { LetterChoice } from '../wizard/LetterChoice'
import { caseRoute } from './caseText'

const buttonLabels: Record<FollowUpKind, string> = {
  reminder: 'Przygotuj ponaglenie',
  'utk-complaint': 'Przygotuj skargę do UTK',
}

/** Why these letters are offered now: the deadline passed, the carrier rejected, or the deadline is still running. */
function hint(item: CaseDto, due: string | null): string {
  if (item.followUps.pastDeadline)
    return `Termin odpowiedzi przewoźnika minął${due ? ` (${due})` : ''}. Jeśli odpowiedź nie nadeszła, wyślij ponaglenie albo złóż skargę do Prezesa Urzędu Transportu Kolejowego.`
  if (item.status === 'Rejected')
    return 'Przewoźnik odrzucił pismo. Możesz złożyć skargę do Prezesa Urzędu Transportu Kolejowego – przygotujemy ją z danych tej sprawy.'
  return `Przewoźnik ma czas na odpowiedź${due ? ` do ${due}` : ''}. Jeśli nie odpowie w terminie, przygotujesz tu ponaglenie, a później skargę do UTK.`
}

/** "Kolejne kroki": the reminder and the complaint to UTK, when the case allows them (rule from the server). */
export function NextSteps({ item }: { item: CaseDto }) {
  const [letter, setLetter] = useState<{ kind: FollowUpKind; dto: ComplaintDto } | null>(null)
  const [busy, setBusy] = useState<FollowUpKind | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirmReminder, setConfirmReminder] = useState(false)
  const abort = useRef<AbortController | null>(null)
  useEffect(() => () => abort.current?.abort(), [])

  const { reminder, utkComplaint, pastDeadline } = item.followUps
  if (!reminder && !utkComplaint) return null

  const due = item.responseDueDate ? formatDate(warsawDate(new Date(item.responseDueDate))) : null
  const kinds = (['reminder', 'utk-complaint'] as const).filter((kind) => (kind === 'reminder' ? reminder : utkComplaint))

  const prepare = async (kind: FollowUpKind) => {
    abort.current?.abort()
    const controller = new AbortController()
    abort.current = controller
    setBusy(kind)
    setError(null)
    setLetter(null)
    try {
      setLetter({ kind, dto: await api.generateFollowUp(item.id, kind, controller.signal) })
    } catch (failure) {
      if (controller.signal.aborted) return
      setError(failure instanceof ApiError ? failure.detail : 'Nie udało się przygotować pisma. Spróbuj ponownie.')
    } finally {
      if (!controller.signal.aborted) setBusy(null)
    }
  }

  return (
    <section className="case-detail__section" aria-labelledby="case-next-title">
      <h2 className="case-detail__subtitle" id="case-next-title">
        Kolejne kroki
      </h2>
      <p className="case-detail__hint">{hint(item, due)}</p>
      <div className="case-detail__actions">
        {kinds.map((kind) => (
          <Button
            key={kind}
            variant={(kind === 'reminder' && !pastDeadline) || (kind === 'utk-complaint' && reminder) ? 'secondary' : 'primary'}
            iconStart={<FileIcon size={18} />}
            loading={busy === kind}
            disabled={busy !== null && busy !== kind}
            onClick={() => (kind === 'reminder' && !pastDeadline ? setConfirmReminder(true) : void prepare(kind))}
          >
            {buttonLabels[kind]}
          </Button>
        ))}
      </div>
      {error && (
        <div className="page-error" role="alert">
          <AlertCircleIcon className="page-error__icon" />
          <div className="page-error__body">
            <p>{error}</p>
          </div>
        </div>
      )}
      {letter && (
        <LetterChoice
          key={letter.kind}
          caseId={item.id}
          letter={letter.dto}
          kind={letter.kind}
          trip={{ carrier: item.carrier, route: caseRoute(item), travelDate: item.travelDate }}
        />
      )}

      <ConfirmDialog
        open={confirmReminder}
        title="Termin odpowiedzi jeszcze trwa"
        confirmLabel="Przygotuj ponaglenie"
        confirmVariant="primary"
        onConfirm={async () => void prepare('reminder')}
        onClose={() => setConfirmReminder(false)}
      >
        <p>
          Przewoźnik ma czas na odpowiedź{due ? ` do ${due}` : ''}. Ponaglenie wysyła się zwykle dopiero po tym terminie.
          Przygotować je mimo to?
        </p>
      </ConfirmDialog>
    </section>
  )
}
