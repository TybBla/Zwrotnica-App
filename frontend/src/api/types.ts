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
}

export interface UpdateProfileRequest {
  fullName: string | null
  address: string | null
  iban: string | null
  preferredPayout: PayoutPreference
}

export interface CarrierOption {
  name: string
  coverage: Verdict
  /** Category codes; empty for a regional carrier. */
  categories: string[]
}

export interface CategoryOption {
  code: string
  label: string
}

export interface ReferenceDataDto {
  carriers: CarrierOption[]
  categories: CategoryOption[]
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
}

export interface AssessmentDto {
  verdict: Verdict
  reason: string
  compensationAmount: number
  percent: number
  belowMinimum: boolean
  priceRequired: boolean
  refusalRisk: boolean
}

/** Body of POST /api/cases and PUT /api/cases/{id}. */
export interface CaseRequest {
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
  payoutPreference: PayoutPreference
  notes: string | null
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
  createdAt: IsoDateTime
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
}

/** The (possibly user-edited) letter to render as PDF. */
export interface ComplaintPdfRequest {
  subject: string
  body: string
}
