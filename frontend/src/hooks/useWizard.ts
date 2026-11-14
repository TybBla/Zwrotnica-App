import { useReducer } from 'react'
import type { AssessmentDto, AssessmentRequest, CarrierOption, DelayCause, ExtractionDto, IncidentType, IsoDate } from '../api/types'
import { finalizeText } from '../lib/textRules'

export type WizardStep = 'start' | 'ticket' | 'delay' | 'result'
export type TicketSource = 'upload' | 'manual'

/** Select value for a carrier outside the reference list; its name is typed into `otherCarrier`. */
export const OTHER_CARRIER = '__other__'

/** The ticket as the user sees it in the form: raw strings, validated by `ticketIssues`, mapped by `toTicketFields`. */
export interface TicketForm {
  /** A reference carrier name, OTHER_CARRIER or '' (not chosen). */
  carrierChoice: string
  otherCarrier: string
  /** Category code (e.g. "IC") or ''. */
  trainCategory: string
  fromStation: string
  toStation: string
  ticketNumber: string
  /** "YYYY-MM-DD" or '' */
  travelDate: string
  /** As typed, e.g. "49,90". */
  price: string
}

export interface DelayInput {
  incidentType: IncidentType
  /** null while empty; ignored for a cancellation. */
  minutes: number | null
  cause?: DelayCause
  isSeasonTicket: boolean
}

/** Extraction field names (ExtractionDto.missingFields) the form highlights until filled. */
export type TicketFieldKey = 'carrier' | 'trainCategory' | 'fromStation' | 'toStation' | 'ticketNumber' | 'ticketPrice' | 'travelDate'

export interface WizardState {
  step: WizardStep
  /** Which way the user last moved, for the step transition. */
  direction: 'forward' | 'back'
  source: TicketSource | null
  /** Ticket files, in memory only – never stored. */
  files: File[]
  ticket: TicketForm
  /** The last successful read of the ticket, if any. */
  extraction?: ExtractionDto
  /** Fields the AI could not read (highlighted while still empty). */
  missing: string[]
  /** Upload path: the review form is shown (after a read). */
  reviewing: boolean
  /** Friendly message after a failed read (the wizard switched to manual entry). */
  notice?: string
  delay: DelayInput
  /** Latest assessment of the current ticket + delay (live preview and the result step). */
  assessment?: AssessmentDto
}

export const emptyTicket: TicketForm = {
  carrierChoice: '',
  otherCarrier: '',
  trainCategory: '',
  fromStation: '',
  toStation: '',
  ticketNumber: '',
  travelDate: '',
  price: '',
}

export const initialWizardState: WizardState = {
  step: 'start',
  direction: 'forward',
  source: null,
  files: [],
  ticket: emptyTicket,
  missing: [],
  reviewing: false,
  delay: { incidentType: 'Delay', minutes: null, isSeasonTicket: false },
}

export type WizardAction =
  | { type: 'start'; source: TicketSource }
  /** Switch between reading the ticket and typing it, inside the ticket step. Keeps everything entered. */
  | { type: 'source'; source: TicketSource }
  | { type: 'files'; files: File[] }
  | { type: 'ticket'; patch: Partial<TicketForm> }
  | { type: 'extracted'; extraction: ExtractionDto; carriers: readonly CarrierOption[] }
  | { type: 'extractionFailed'; notice: string }
  | { type: 'dismissNotice' }
  | { type: 'delay'; patch: Partial<DelayInput> }
  | { type: 'assessed'; assessment: AssessmentDto | undefined }
  | { type: 'next' }
  | { type: 'back' }
  | { type: 'reset' }

const order: WizardStep[] = ['start', 'ticket', 'delay', 'result']

/** "49,90" → 49.9 (decimal comma or dot, at most 2 decimals); null when it is not a plain amount. */
export function parsePrice(text: string): number | null {
  const compact = text.replace(/\s/g, '')
  if (!/^\d+([.,]\d{1,2})?$/.test(compact)) return null
  return Number(compact.replace(',', '.'))
}

/** 49.9 → "49,90", the way a Polish user types it. */
export function formatPriceInput(value: number): string {
  return value.toFixed(2).replace('.', ',')
}

/** Today's date in the user's time zone, "YYYY-MM-DD". */
export function todayIso(now = new Date()): IsoDate {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

export interface TicketIssue {
  field: 'carrier' | 'travelDate' | 'price'
  /** Polish, for the field's error line. */
  message: string
  /** True when the field is simply empty (shown as "still to fill", not as an error). */
  empty: boolean
}

const isRealDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(`${value}T00:00:00Z`))

/** What still blocks "Dalej": only carrier, travel date and price are required. */
export function ticketIssues(ticket: TicketForm, today: IsoDate = todayIso()): TicketIssue[] {
  const issues: TicketIssue[] = []

  if (ticket.carrierChoice === '') issues.push({ field: 'carrier', message: 'Wybierz przewoźnika.', empty: true })
  else if (ticket.carrierChoice === OTHER_CARRIER && ticket.otherCarrier.trim() === '')
    issues.push({ field: 'carrier', message: 'Wpisz nazwę przewoźnika.', empty: true })

  if (ticket.travelDate === '') issues.push({ field: 'travelDate', message: 'Podaj datę przejazdu.', empty: true })
  else if (!isRealDate(ticket.travelDate)) issues.push({ field: 'travelDate', message: 'Podaj poprawną datę.', empty: false })
  else if (ticket.travelDate > today)
    issues.push({ field: 'travelDate', message: 'Data przejazdu nie może być z przyszłości.', empty: false })

  const price = parsePrice(ticket.price)
  if (ticket.price.trim() === '') issues.push({ field: 'price', message: 'Podaj cenę biletu.', empty: true })
  else if (price === null) issues.push({ field: 'price', message: 'Wpisz kwotę, np. 49,90.', empty: false })
  else if (price <= 0) issues.push({ field: 'price', message: 'Cena musi być większa niż 0 zł.', empty: false })
  else if (price > 100_000) issues.push({ field: 'price', message: 'Ta kwota wygląda na zbyt wysoką – sprawdź ją.', empty: false })

  return issues
}

const optional = (value: string, field: 'station' | 'ticketNumber' | 'carrier' | 'trainCategory') =>
  finalizeText(field, value) || null

/** The ticket in API shape (fields of AssessmentRequest / CaseRequest); empty optional text becomes null. */
export function toTicketFields(ticket: TicketForm) {
  return {
    carrier: ticket.carrierChoice === OTHER_CARRIER ? optional(ticket.otherCarrier, 'carrier') : ticket.carrierChoice || null,
    trainCategory: optional(ticket.trainCategory, 'trainCategory'),
    fromStation: optional(ticket.fromStation, 'station'),
    toStation: optional(ticket.toStation, 'station'),
    ticketNumber: optional(ticket.ticketNumber, 'ticketNumber'),
    travelDate: ticket.travelDate || null,
    ticketPrice: parsePrice(ticket.price),
  }
}

/** The request for live assessment, or null while price or minutes are not usable yet. */
export function toAssessmentRequest(state: Pick<WizardState, 'ticket' | 'delay'>): AssessmentRequest | null {
  const { carrier, trainCategory, ticketPrice } = toTicketFields(state.ticket)
  const { incidentType, minutes, cause, isSeasonTicket } = state.delay
  if (ticketPrice === null || ticketPrice <= 0) return null
  if (incidentType === 'Delay' && minutes === null) return null
  return {
    carrier,
    trainCategory,
    incidentType,
    delayMinutes: incidentType === 'Cancellation' ? 0 : (minutes ?? 0),
    ticketPrice,
    delayCause: cause ?? null,
    isSeasonTicket,
  }
}

/** Merges what the AI read into the form: read values win, unread ones keep whatever the user already typed. */
function mergeExtraction(ticket: TicketForm, extraction: ExtractionDto, carriers: readonly CarrierOption[]): TicketForm {
  const next = { ...ticket }
  if (extraction.carrier) {
    if (carriers.some((carrier) => carrier.name === extraction.carrier)) {
      next.carrierChoice = extraction.carrier
    } else {
      next.carrierChoice = OTHER_CARRIER
      next.otherCarrier = extraction.carrier
    }
  }
  if (extraction.trainCategory) next.trainCategory = extraction.trainCategory
  if (extraction.fromStation) next.fromStation = extraction.fromStation
  if (extraction.toStation) next.toStation = extraction.toStation
  if (extraction.ticketNumber) next.ticketNumber = extraction.ticketNumber
  if (extraction.travelDate) next.travelDate = extraction.travelDate
  if (extraction.ticketPrice !== null) next.price = formatPriceInput(extraction.ticketPrice)
  // Regional carriers have no categories: a printed one would only confuse the case.
  if (carriers.find((carrier) => carrier.name === next.carrierChoice)?.coverage === 'NotCovered') next.trainCategory = ''
  return next
}

export function wizardReducer(state: WizardState, action: WizardAction): WizardState {
  switch (action.type) {
    case 'start':
      return { ...state, step: 'ticket', direction: 'forward', source: action.source, notice: undefined }
    case 'source':
      return { ...state, source: action.source, reviewing: false, notice: undefined }
    case 'files':
      return { ...state, files: action.files }
    case 'ticket':
      // The ticket decides eligibility and amount, so an older assessment no longer applies.
      return { ...state, ticket: { ...state.ticket, ...action.patch }, assessment: undefined }
    case 'extracted':
      return {
        ...state,
        ticket: mergeExtraction(state.ticket, action.extraction, action.carriers),
        extraction: action.extraction,
        missing: action.extraction.missingFields,
        reviewing: true,
        notice: undefined,
        assessment: undefined,
      }
    case 'extractionFailed':
      return { ...state, source: 'manual', reviewing: false, notice: action.notice }
    case 'dismissNotice':
      return { ...state, notice: undefined }
    case 'delay':
      return { ...state, delay: { ...state.delay, ...action.patch } }
    case 'assessed':
      return { ...state, assessment: action.assessment }
    case 'next': {
      const index = order.indexOf(state.step)
      return { ...state, step: order[Math.min(index + 1, order.length - 1)], direction: 'forward' }
    }
    case 'back': {
      const index = order.indexOf(state.step)
      return { ...state, step: order[Math.max(index - 1, 0)], direction: 'back' }
    }
    case 'reset':
      return { ...initialWizardState, direction: 'back' }
  }
}

/**
 * Wizard state (Bilet → Opóźnienie → Wynik). Everything entered survives moving back and forth and failed reads;
 * the result step (Task 10) reads `source`, `ticket` (via `toTicketFields`), `delay`, `assessment` and `extraction`.
 */
export function useWizard() {
  const [state, dispatch] = useReducer(wizardReducer, initialWizardState)
  return { state, dispatch }
}
