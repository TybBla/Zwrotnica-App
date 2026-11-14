// Mirrors backend/src/RefundPilot.Application/Dtos (JSON camelCase, enums as strings,
// DateOnly as "YYYY-MM-DD", DateTime as ISO-8601 UTC, decimal as number).

export type PayoutPreference = 'OriginalPayment' | 'BankTransfer' | 'Voucher'
export type Verdict = 'Eligible' | 'NotCovered' | 'Unknown'
export type IncidentType = 'Delay' | 'Cancellation'
export type CaseStatus = 'New' | 'Sent' | 'Paid' | 'Rejected'
export type DelayCause = 'Unknown' | 'Weather' | 'PersonsOnTrack' | 'RailStrike' | 'Technical' | 'Other'

/** "YYYY-MM-DD" */
export type IsoDate = string
/** ISO-8601 date-time, UTC */
export type IsoDateTime = string

export interface UserInfo {
  email: string
  isEmailConfirmed: boolean
}

export interface ProfileDto {
  email: string
  fullName: string | null
  address: string | null
  iban: string | null
  preferredPayout: PayoutPreference
  /** One e-mail per case once the carrier's month to answer has passed. */
  emailReminders: boolean
}

export interface UpdateProfileRequest {
  fullName: string | null
  address: string | null
  iban: string | null
  preferredPayout: PayoutPreference
  emailReminders: boolean
}

export interface CarrierOption {
  name: string
  coverage: Verdict
  /** Category codes; empty for a regional carrier. */
  categories: string[]
  /** Official complaints page (or home page) verified on the carrier's site; null/absent when none was verified. */
  complaintUrl?: string | null
  /** Official complaint form (PDF), when one was verified. */
  complaintFormUrl?: string | null
  /** How the carrier accepts complaints, as verified on its site. */
  complaintChannels?: string | null
}

/** Where a complaint to the rail regulator goes. */
export interface ComplaintOfficeDto {
  name: string
  complaintUrl: string
  complaintChannels: string
}

export interface CategoryOption {
  code: string
  label: string
}

export interface ReferenceDataDto {
  carriers: CarrierOption[]
  categories: CategoryOption[]
  /** Carrier's minimum payout in PLN; smaller amounts are flagged as possibly not paid out. */
  minimumPayout: number
  utk: ComplaintOfficeDto
}

export interface ExtractionDto {
  carrier: string | null
  trainCategory: string | null
  fromStation: string | null
  toStation: string | null
  ticketNumber: string | null
  ticketPrice: number | null
  travelDate: IsoDate | null
  missingFields: string[]
  usedAi: boolean
}

export interface AssessmentRequest {
  carrier: string | null
  trainCategory: string | null
  incidentType: IncidentType
  delayMinutes: number
  ticketPrice: number
  delayCause: DelayCause | null
  isSeasonTicket: boolean
  /** Documented costs (regional carriers only); stated in the final verdict. */
  expenseAmount?: number
}

/** "Werdykt końcowy": the expected legal effect of the complaint, worded by the server from its rules. */
export interface OutcomeDto {
  title: string
  statement: string
  legalBasis: string | null
  deadline: string | null
}

export interface AssessmentDto {
  verdict: Verdict
  reason: string
  compensationAmount: number
  percent: number
  belowMinimum: boolean
  priceRequired: boolean
  refusalRisk: boolean
  /** null while the ticket price is missing. */
  outcome: OutcomeDto | null
}

/** Body of POST /api/cases and PUT /api/cases/{id}. */
export interface CaseRequest {
  carrier: string
  trainCategory: string | null
  fromStation: string | null
  toStation: string | null
  ticketNumber: string
  travelDate: IsoDate
  ticketPrice: number
  incidentType: IncidentType
  delayMinutes: number
  delayCause: DelayCause | null
  isSeasonTicket: boolean
  payoutPreference: PayoutPreference
  notes: string | null
  /** Costs the passenger incurred (regional carriers only; the server drops them otherwise). */
  expenseDescription?: string | null
  /** PLN, 0 < x ≤ 100 000. */
  expenseAmount?: number
}

export interface CaseDto {
  id: number
  carrier: string
  trainCategory: string | null
  fromStation: string | null
  toStation: string | null
  ticketNumber: string | null
  travelDate: IsoDate
  ticketPrice: number
  incidentType: IncidentType
  delayMinutes: number
  delayCause: DelayCause | null
  isSeasonTicket: boolean
  verdict: Verdict
  refusalRisk: boolean
  compensationAmount: number
  payoutPreference: PayoutPreference
  status: CaseStatus
  filedAt: IsoDateTime | null
  responseDueDate: IsoDateTime | null
  notes: string | null
  expenseDescription: string | null
  expenseAmount: number | null
  createdAt: IsoDateTime
  /** The same final verdict the wizard shows; null without a ticket price. */
  outcome: OutcomeDto | null
  /** Which follow-up letters the case allows now (server rule, Warsaw dates). */
  followUps: FollowUpsDto
}

/** Letters after the first one: ponaglenie to the carrier, skarga do Prezesa UTK. */
export type FollowUpKind = 'reminder' | 'utk-complaint'

export interface FollowUpsDto {
  reminder: boolean
  utkComplaint: boolean
  /** Sent and the carrier's deadline has passed. */
  pastDeadline: boolean
}

export interface StatsDto {
  recovered: number
  pending: number
  totalCases: number
}

export interface ComplaintRequest {
  payout: PayoutPreference
  /** Used for this letter only, unless saveIbanToProfile is true. */
  oneTimeIban?: string | null
  saveIbanToProfile: boolean
}

export interface ComplaintDto {
  subject: string
  body: string
  usedAi: boolean
  /** Polish labels of placeholders left in the letter for the user to fill in. */
  missingFields: string[]
  /** True when this letter moved a new case to Sent: the carrier's month to answer starts today. */
  deadlineStarted: boolean
  responseDueDate: IsoDateTime | null
}

/** The (possibly user-edited) letter to render as PDF. */
export interface ComplaintPdfRequest {
  subject: string
  body: string
}
