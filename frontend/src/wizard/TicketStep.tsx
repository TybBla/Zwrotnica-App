import { useEffect, useLayoutEffect, useRef, useState, type Dispatch } from 'react'
import { ApiError, api } from '../api/client'
import type { CarrierOption, ReferenceDataDto } from '../api/types'
import { Button } from '../components/Button'
import { Field } from '../components/Field'
import { AlertCircleIcon, ArrowRightIcon, CloseIcon, InfoIcon, PencilIcon, ShieldIcon, ScanIcon, UploadIcon } from '../components/icons'
import { Select, type SelectOption } from '../components/Select'
import { TextInput } from '../components/TextInput'
import { STATIONS } from '../lib/stations'
import {
  OTHER_CARRIER,
  ticketIssues,
  todayIso,
  type TicketFieldKey,
  type TicketForm,
  type TicketIssue,
  type WizardAction,
  type WizardState,
} from '../hooks/useWizard'
import { TicketSourcePicker } from './TicketSourcePicker'
import { WizardActions } from './WizardActions'

interface TicketStepProps {
  state: WizardState
  dispatch: Dispatch<WizardAction>
  /** null while loading or when it failed (then only "Inny" is offered). */
  reference: ReferenceDataDto | null
}

const MISSING_HINT = 'Uzupełnij – nie udało się odczytać'
const STATION_LIST_ID = 'station-suggestions'
const TICKET_NUMBER_HINT = 'Numer znajdziesz na bilecie (np. przy kodzie QR lub w e-mailu z biletem).'

/** A failed read never strands the user: say what happened in plain words and continue with manual entry. */
function extractionNotice(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return 'Odczyt biletu przez AI jest teraz wyłączony. Uzupełnij dane poniżej – to zajmie chwilę.'
    if (error.status === 502) return 'AI jest chwilowo niedostępne i nie odczytało biletu. Uzupełnij dane poniżej albo spróbuj później.'
    if (error.status === 429) return 'Wykorzystano limit odczytów. Spróbuj ponownie za minutę albo uzupełnij dane poniżej.'
    return `${error.detail} Uzupełnij dane poniżej.`
  }
  return 'Nie udało się odczytać biletu. Uzupełnij dane poniżej.'
}

/** Form order, with the labels used in "Nie udało się odczytać: …". */
const unreadLabels: [TicketFieldKey, string][] = [
  ['carrier', 'przewoźnik'],
  ['trainCategory', 'kategoria pociągu'],
  ['travelDate', 'data przejazdu'],
  ['ticketPrice', 'cena biletu'],
  ['fromStation', 'stacja początkowa'],
  ['toStation', 'stacja docelowa'],
  ['ticketNumber', 'numer biletu'],
]

const keyFields: TicketFieldKey[] = ['carrier', 'travelDate', 'ticketPrice']

function formValue(ticket: TicketForm, key: TicketFieldKey): string {
  switch (key) {
    case 'carrier':
      return ticket.carrierChoice === OTHER_CARRIER ? ticket.otherCarrier : ticket.carrierChoice
    case 'ticketPrice':
      return ticket.price
    default:
      return ticket[key]
  }
}

/**
 * What the last read could not get and the form still lacks. A regional carrier has no category, so a missing one
 * is not worth mentioning. `nothingRead`: carrier, date and price are all missing – probably a blurred or cut-off photo.
 */
function unreadSummary(ticket: TicketForm, missing: string[], regional: boolean) {
  const unread = unreadLabels.filter(
    ([key]) => missing.includes(key) && formValue(ticket, key).trim() === '' && !(regional && key === 'trainCategory'),
  )
  const keys = unread.map(([key]) => key)
  return { labels: unread.map(([, label]) => label), nothingRead: keyFields.every((key) => keys.includes(key)) }
}

const isInterRegio = (category: string) => /^s*(ir|interregio)s*$/i.test(category)

const requiredNames: Record<TicketIssue['field'], string> = {
  carrier: 'przewoźnika',
  travelDate: 'datę przejazdu',
  price: 'cenę biletu',
  ticketNumber: 'numer biletu',
}

export function TicketStep({ state, dispatch, reference }: TicketStepProps) {
  const [extracting, setExtracting] = useState(false)
  const [fileError, setFileError] = useState<string | null>(null)
  const picking = state.source === 'upload' && !state.reviewing
  const reading = useRef<AbortController | null>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)

  const cancelRead = () => {
    reading.current?.abort()
    reading.current = null
  }

  // Leaving the step cancels a read that is still running (the reducer also ignores a result that lands late).
  useEffect(() => cancelRead, [])

  // The picker and the form replace each other inside this step: the control that had focus disappears, so the
  // heading of the new content takes it (instead of focus falling back to <body>).
  const shown = useRef({ source: state.source, reviewing: state.reviewing })
  useLayoutEffect(() => {
    const previous = shown.current
    if (previous.source === state.source && previous.reviewing === state.reviewing) return
    shown.current = { source: state.source, reviewing: state.reviewing }
    headingRef.current?.focus({ preventScroll: true })
  }, [state.source, state.reviewing])

  const read = async () => {
    cancelRead()
    const controller = new AbortController()
    reading.current = controller
    setExtracting(true)
    setFileError(null)
    try {
      const extraction = await api.extractTicket(state.files, controller.signal)
      if (controller.signal.aborted) return
      dispatch({ type: 'extracted', extraction, carriers: reference?.carriers ?? [] })
    } catch (error) {
      if (controller.signal.aborted) return
      // 400 = something is wrong with the files themselves: stay here so the user can swap them.
      if (error instanceof ApiError && error.status === 400) setFileError(error.detail)
      else dispatch({ type: 'extractionFailed', notice: extractionNotice(error) })
    } finally {
      if (reading.current === controller) {
        reading.current = null
        setExtracting(false)
      }
    }
  }

  const switchSource = (source: 'upload' | 'manual') => {
    cancelRead()
    setExtracting(false)
    dispatch({ type: 'source', source })
  }

  const back = () => {
    cancelRead()
    dispatch({ type: 'back' })
  }

  const regionalCarrier = reference?.carriers.find((c) => c.name === state.ticket.carrierChoice)?.coverage === 'NotCovered'
  const unread = state.reviewing && !picking ? unreadSummary(state.ticket, state.missing, regionalCarrier) : null
  const issues = ticketIssues(state.ticket)
  const missingRequired = issues.filter((issue) => issue.empty).map((issue) => requiredNames[issue.field])
  const nextHint =
    issues.length === 0
      ? undefined
      : missingRequired.length === issues.length
        ? `Aby przejść dalej, podaj ${joinPolish(missingRequired)}.`
        : 'Popraw zaznaczone pola, aby przejść dalej.'

  return (
    <div className="wizard-step">
      <header className="wizard-step__head">
        <h2 className="wizard-step__title" tabIndex={-1} ref={headingRef}>
          {picking ? 'Dodaj bilet' : state.reviewing ? 'Sprawdź dane z biletu' : 'Dane z biletu'}
        </h2>
        <p className="wizard-step__lead">
          {picking
            ? 'Zrób zdjęcie biletu albo dodaj e-bilet w PDF. Odczytamy dane, a Ty je sprawdzisz.'
            : state.reviewing
              ? 'Odczytaliśmy, co się dało. AI może się pomylić – rzuć okiem i popraw, jeśli trzeba.'
              : 'Wystarczą przewoźnik, data, cena i numer biletu. Resztę możesz pominąć.'}
        </p>
      </header>

      {state.notice && (
        <div className="notice" role="status" aria-labelledby="ticket-notice-title">
          <AlertCircleIcon className="notice__icon" />
          <div className="notice__body">
            <p className="notice__title" id="ticket-notice-title">
              Wpisz dane ręcznie
            </p>
            <p className="notice__text">{state.notice}</p>
          </div>
          <button type="button" className="notice__close" aria-label="Zamknij komunikat" onClick={() => dispatch({ type: 'dismissNotice' })}>
            <CloseIcon size={18} />
          </button>
        </div>
      )}

      {unread && unread.labels.length > 0 && (
        <div className="notice" role="status" aria-labelledby="ticket-unread-title">
          <AlertCircleIcon className="notice__icon" />
          <div className="notice__body">
            <p className="notice__title" id="ticket-unread-title">
              {unread.nothingRead
                ? 'Nie udało się odczytać biletu – zdjęcie może być nieostre lub ucięte.'
                : `Nie udało się odczytać: ${joinPolish(unread.labels)}.`}
            </p>
            <p className="notice__text">
              {unread.nothingRead
                ? 'Wgraj wyraźniejsze zdjęcie albo wpisz dane ręcznie.'
                : 'Wgraj wyraźniejsze zdjęcie albo uzupełnij te pola ręcznie.'}
            </p>
            <Button variant="secondary" size="sm" className="notice__action" iconStart={<UploadIcon size={16} />} onClick={() => switchSource('upload')}>
              Wgraj inne zdjęcie
            </Button>
          </div>
        </div>
      )}

      {picking ? (
        <div className="wizard-swap stack" key="pick">
          <TicketSourcePicker files={state.files} onFilesChange={(files) => dispatch({ type: 'files', files })} disabled={extracting} />
          {fileError && (
            <p className="inline-error" role="alert">
              <AlertCircleIcon size={16} />
              {fileError}
            </p>
          )}
          <div className="wizard-notes">
            <p>
              <InfoIcon size={16} />
              Wersja demo korzysta z darmowego modelu AI – odczyt może potrwać kilka sekund i bywa niedokładny.
            </p>
            <p>
              <ShieldIcon size={16} />
              Nie zapisujemy plików – służą tylko do odczytania danych z biletu.
            </p>
          </div>
          <p className="visually-hidden" role="status">
            {extracting ? 'Odczytuję bilet, to potrwa kilka sekund.' : ''}
          </p>
          <button type="button" className="link-button" onClick={() => switchSource('manual')}>
            <PencilIcon size={16} />
            Wpisz dane ręcznie
          </button>
        </div>
      ) : (
        <div className="wizard-swap stack" key="form">
          {state.reviewing ? (
            <div className="source-summary">
              <ScanIcon className="source-summary__icon" size={18} />
              <p className="source-summary__text">
                Odczytano z {state.files.length === 1 ? '1 pliku' : `${state.files.length} plików`}
                {state.missing.length > 0 && ' · uzupełnij zaznaczone pola'}
              </p>
              <Button variant="ghost" size="sm" onClick={() => switchSource('upload')}>
                Zmień pliki
              </Button>
            </div>
          ) : (
            <button type="button" className="link-button" onClick={() => switchSource('upload')}>
              <UploadIcon size={16} />
              Wgraj bilet – odczytamy dane za Ciebie
            </button>
          )}
          <TicketForm
            ticket={state.ticket}
            missing={state.missing}
            issues={issues}
            reference={reference}
            onChange={(patch) => dispatch({ type: 'ticket', patch })}
          />
        </div>
      )}

      {picking ? (
        <WizardActions onBack={back}>
          {() => (
            <Button
              className="wizard-actions__primary"
              iconStart={<ScanIcon size={18} />}
              disabled={state.files.length === 0}
              loading={extracting}
              onClick={read}
            >
              Odczytaj dane z biletu
            </Button>
          )}
        </WizardActions>
      ) : (
        <WizardActions onBack={back} hint={nextHint}>
          {(hintId) => (
            <Button
              className="wizard-actions__primary"
              iconEnd={<ArrowRightIcon size={18} />}
              disabled={issues.length > 0}
              aria-describedby={hintId}
              onClick={() => dispatch({ type: 'next' })}
            >
              Dalej
            </Button>
          )}
        </WizardActions>
      )}
    </div>
  )
}

function joinPolish(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} i ${items[items.length - 1]}`
}

interface TicketFormProps {
  ticket: TicketForm
  missing: string[]
  issues: TicketIssue[]
  reference: ReferenceDataDto | null
  onChange: (patch: Partial<TicketForm>) => void
}

function TicketForm({ ticket, missing, issues, reference, onChange }: TicketFormProps) {
  const [touched, setTouched] = useState<ReadonlySet<string>>(new Set())
  const touch = (field: string) => setTouched((current) => (current.has(field) ? current : new Set(current).add(field)))
  const today = todayIso()

  const carriers = reference?.carriers ?? []
  const carrier: CarrierOption | undefined = carriers.find((option) => option.name === ticket.carrierChoice)
  const regional = carrier?.coverage === 'NotCovered'

  /** Highlighted while the AI could not read it and the user has not filled it yet. */
  const attention = (key: TicketFieldKey, value: string) => missing.includes(key) && value.trim() === ''
  /** Format errors (not "still empty") once the user has left the field; dates as soon as they are picked. */
  const errorFor = (field: TicketIssue['field'], showNow = false) => {
    const issue = issues.find((candidate) => candidate.field === field)
    return issue && !issue.empty && (showNow || touched.has(field)) ? issue.message : null
  }

  const carrierOptions: SelectOption[] = [
    ...carriers.map((option) => ({
      value: option.name,
      label: option.coverage === 'NotCovered' ? `${option.name} (regionalny)` : option.name,
    })),
    { value: OTHER_CARRIER, label: 'Inny' },
  ]

  const labels = new Map((reference?.categories ?? []).map((category) => [category.code, category.label]))
  const codes = carrier ? carrier.categories : [...labels.keys()]
  const categoryOptions: SelectOption[] = [
    { value: '', label: 'Nie wiem / brak na bilecie' },
    ...codes.map((code) => ({ value: code, label: labels.has(code) ? `${code} – ${labels.get(code)}` : code })),
  ]
  if (ticket.trainCategory && !codes.includes(ticket.trainCategory))
    categoryOptions.push({ value: ticket.trainCategory, label: ticket.trainCategory })

  const chooseCarrier = (value: string) => {
    const option = carriers.find((candidate) => candidate.name === value)
    const keepCategory = !option || option.categories.includes(ticket.trainCategory)
    onChange({ carrierChoice: value, ...(keepCategory ? {} : { trainCategory: '' }) })
  }

  const carrierAttention = attention('carrier', ticket.carrierChoice === OTHER_CARRIER ? ticket.otherCarrier : ticket.carrierChoice)
  const priceAttention = attention('ticketPrice', ticket.price)
  const dateAttention = attention('travelDate', ticket.travelDate)
  const ticketNumberAttention = attention('ticketNumber', ticket.ticketNumber)

  return (
    <div className="ticket-form">
      <Field
        label="Przewoźnik"
        required
        attention={carrierAttention}
        hint={carrierAttention ? MISSING_HINT : undefined}
        note={
          regional
            ? `${carrier.name} to przewoźnik regionalny. Przepisy UE o odszkodowaniu 25% / 50% go nie obejmują, więc nie wyliczymy kwoty. Możesz kontynuować i przygotować reklamację – przewoźnik rozpatrzy ją według swojego regulaminu.`
            : undefined
        }
      >
        <Select value={ticket.carrierChoice} onValueChange={chooseCarrier} options={carrierOptions} placeholder="Wybierz przewoźnika" />
      </Field>

      {ticket.carrierChoice === OTHER_CARRIER && (
        <Field label="Nazwa przewoźnika" required error={errorFor('carrier')}>
          <TextInput
            field="carrier"
            maxLength={200}
            value={ticket.otherCarrier}
            onValueChange={(otherCarrier) => onChange({ otherCarrier })}
            onBlur={() => touch('carrier')}
            placeholder="np. Leo Express"
            autoComplete="off"
          />
        </Field>
      )}

      {!regional && (
        <Field
          label="Kategoria pociągu"
          optional
          attention={attention('trainCategory', ticket.trainCategory)}
          hint={attention('trainCategory', ticket.trainCategory) ? MISSING_HINT : undefined}
          note={
            isInterRegio(ticket.trainCategory)
              ? 'Do sprawdzenia: pociągi InterRegio prowadzi przewoźnik regionalny Polregio, więc przepisy UE o odszkodowaniu 25% / 50% mogą ich nie obejmować i kwoty nie wyliczymy. Możesz kontynuować i przygotować reklamację – przewoźnik rozpatrzy ją według swojego regulaminu.'
              : undefined
          }
          tooltip="Skrót przed numerem pociągu na bilecie, np. „IC 5310”. Dla PKP Intercity od kategorii zależy, czy należy się rekompensata."
        >
          <Select value={ticket.trainCategory} onValueChange={(trainCategory) => onChange({ trainCategory })} options={categoryOptions} />
        </Field>
      )}

      <div className="ticket-form__row">
        <Field
          label="Data przejazdu"
          required
          attention={dateAttention}
          error={errorFor('travelDate', true)}
          hint={dateAttention ? MISSING_HINT : undefined}
        >
          <TextInput type="date" value={ticket.travelDate} max={today} onValueChange={(travelDate) => onChange({ travelDate })} />
        </Field>

        <Field
          label="Cena biletu"
          required
          attention={priceAttention}
          error={errorFor('price')}
          hint={priceAttention ? MISSING_HINT : 'Tyle, ile faktycznie zapłaciłeś (z ulgą).'}
        >
          <TextInput
            value={ticket.price}
            onValueChange={(price) => onChange({ price: price.replace(/[^\d.,]/g, '') })}
            onBlur={() => touch('price')}
            inputMode="decimal"
            enterKeyHint="next"
            placeholder="np. 49,90"
            autoComplete="off"
            suffix="zł"
            className="tabular"
          />
        </Field>
      </div>

      <div className="ticket-form__row">
        <Field
          label="Stacja początkowa"
          optional
          attention={attention('fromStation', ticket.fromStation)}
          hint={attention('fromStation', ticket.fromStation) ? MISSING_HINT : undefined}
        >
          <TextInput field="station" maxLength={200} value={ticket.fromStation} onValueChange={(fromStation) => onChange({ fromStation })} placeholder="np. Kraków Główny" autoComplete="off" list={STATION_LIST_ID} />
        </Field>
        <Field
          label="Stacja docelowa"
          optional
          attention={attention('toStation', ticket.toStation)}
          hint={attention('toStation', ticket.toStation) ? MISSING_HINT : undefined}
        >
          <TextInput field="station" maxLength={200} value={ticket.toStation} onValueChange={(toStation) => onChange({ toStation })} placeholder="np. Warszawa Centralna" autoComplete="off" list={STATION_LIST_ID} />
        </Field>
        {/* Native suggestions: a hint while typing, any other station can still be entered. */}
        <datalist id={STATION_LIST_ID}>
          {STATIONS.map((station) => (
            <option key={station} value={station} />
          ))}
        </datalist>
      </div>

      <Field
        label="Numer biletu"
        required
        attention={ticketNumberAttention}
        hint={
          ticketNumberAttention
            ? `${MISSING_HINT}. ${TICKET_NUMBER_HINT}`
            : TICKET_NUMBER_HINT
        }
      >
        <TextInput
          field="ticketNumber"
          maxLength={100}
          value={ticket.ticketNumber}
          onValueChange={(ticketNumber) => onChange({ ticketNumber })}
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          className="mono"
        />
      </Field>
    </div>
  )
}
