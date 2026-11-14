import { describe, expect, it } from 'vitest'
import {
  OTHER_CARRIER,
  emptyTicket,
  initialWizardState,
  parsePrice,
  ticketIssues,
  toAssessmentRequest,
  toTicketFields,
  wizardReducer,
  type TicketForm,
} from './useWizard'

const today = '2026-11-14'

const filled: TicketForm = {
  ...emptyTicket,
  carrierChoice: 'PKP Intercity',
  trainCategory: 'IC',
  fromStation: 'Kraków Główny ',
  travelDate: '2026-11-10',
  price: '49,90',
}

describe('parsePrice', () => {
  it('accepts a decimal comma or dot', () => {
    expect(parsePrice('49,90')).toBe(49.9)
    expect(parsePrice('12.5')).toBe(12.5)
    expect(parsePrice(' 120 ')).toBe(120)
  })

  it('rejects anything that is not a plain amount', () => {
    expect(parsePrice('')).toBeNull()
    expect(parsePrice('1,2,3')).toBeNull()
    expect(parsePrice('12,345')).toBeNull()
    expect(parsePrice(',')).toBeNull()
  })
})

describe('ticketIssues', () => {
  it('requires carrier, travel date and price only', () => {
    expect(ticketIssues(emptyTicket, today).map((issue) => issue.field)).toEqual(['carrier', 'travelDate', 'price'])
    expect(ticketIssues(filled, today)).toEqual([])
  })

  it('requires the other carrier name when "Inny" is chosen', () => {
    expect(ticketIssues({ ...filled, carrierChoice: OTHER_CARRIER, otherCarrier: ' ' }, today)[0].field).toBe('carrier')
    expect(ticketIssues({ ...filled, carrierChoice: OTHER_CARRIER, otherCarrier: 'Leo Express' }, today)).toEqual([])
  })

  it('rejects a future date and a zero price', () => {
    expect(ticketIssues({ ...filled, travelDate: '2026-11-15' }, today)[0].message).toMatch(/przyszłości/)
    expect(ticketIssues({ ...filled, price: '0' }, today)[0].field).toBe('price')
  })
})

describe('request mapping', () => {
  it('maps the form to API fields with trimmed optional text and nulls for empty values', () => {
    expect(toTicketFields(filled)).toEqual({
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      fromStation: 'Kraków Główny',
      toStation: null,
      ticketNumber: null,
      travelDate: '2026-11-10',
      ticketPrice: 49.9,
    })
  })

  it('builds the assessment request from ticket and delay', () => {
    let state = wizardReducer(initialWizardState, { type: 'start', source: 'manual' })
    state = wizardReducer(state, { type: 'ticket', patch: filled })
    state = wizardReducer(state, { type: 'delay', patch: { minutes: 75 } })
    expect(toAssessmentRequest(state)).toEqual({
      carrier: 'PKP Intercity',
      trainCategory: 'IC',
      incidentType: 'Delay',
      delayMinutes: 75,
      ticketPrice: 49.9,
      delayCause: null,
      isSeasonTicket: false,
    })
    state = wizardReducer(state, { type: 'delay', patch: { incidentType: 'Cancellation', minutes: null } })
    expect(toAssessmentRequest(state)?.delayMinutes).toBe(0)
    expect(toAssessmentRequest(wizardReducer(state, { type: 'delay', patch: { incidentType: 'Delay' } }))).toBeNull()
  })
})

describe('wizardReducer', () => {
  it('fills only what the AI read and remembers the rest as missing', () => {
    let state = wizardReducer(initialWizardState, { type: 'start', source: 'upload' })
    state = wizardReducer(state, { type: 'ticket', patch: { price: '30' } })
    state = wizardReducer(state, {
      type: 'extracted',
      extraction: {
        carrier: 'Leo Express',
        trainCategory: null,
        fromStation: 'Kraków Główny',
        toStation: null,
        ticketNumber: 'AB-1',
        ticketPrice: null,
        travelDate: '2026-11-01',
        missingFields: ['trainCategory', 'toStation', 'ticketPrice'],
        usedAi: true,
      },
      carriers: [{ name: 'PKP Intercity', coverage: 'Eligible', categories: ['IC'] }],
    })
    expect(state.ticket).toMatchObject({
      carrierChoice: OTHER_CARRIER,
      otherCarrier: 'Leo Express',
      fromStation: 'Kraków Główny',
      ticketNumber: 'AB-1',
      travelDate: '2026-11-01',
      price: '30',
    })
    expect(state.missing).toEqual(['trainCategory', 'toStation', 'ticketPrice'])
    expect(state.reviewing).toBe(true)
  })

  it('falls back to manual entry on an extraction error without losing data', () => {
    let state = wizardReducer(initialWizardState, { type: 'start', source: 'upload' })
    state = wizardReducer(state, { type: 'ticket', patch: { price: '30' } })
    state = wizardReducer(state, { type: 'extractionFailed', notice: 'AI niedostępne' })
    expect(state.source).toBe('manual')
    expect(state.notice).toBe('AI niedostępne')
    expect(state.ticket.price).toBe('30')
  })

  it('drops a stale assessment when the ticket changes', () => {
    let state = wizardReducer(initialWizardState, { type: 'start', source: 'manual' })
    state = wizardReducer(state, {
      type: 'assessed',
      assessment: { verdict: 'Eligible', reason: '', compensationAmount: 10, percent: 25, belowMinimum: false, priceRequired: false, refusalRisk: false },
    })
    expect(state.assessment).toBeDefined()
    state = wizardReducer(state, { type: 'ticket', patch: { price: '40' } })
    expect(state.assessment).toBeUndefined()
  })
})
