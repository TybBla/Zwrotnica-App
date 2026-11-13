import { describe, expect, it, vi } from 'vitest'
import { ApiError, api } from './client'

function problem(status: number, body: Record<string, unknown>) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function stubFetch(response: Response | Error) {
  const fetchMock = vi.fn(() =>
    response instanceof Error ? Promise.reject(response) : Promise.resolve(response),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function caught(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    if (error instanceof ApiError) return error
    throw error
  }
  throw new Error('expected the call to fail')
}

describe('api client', () => {
  it('maps a 502 ProblemDetails to ApiError carrying the server detail', async () => {
    stubFetch(problem(502, { status: 502, title: 'Bad Gateway', detail: 'AI nie odpowiada – wpisz dane ręcznie.' }))

    const error = await caught(api.extractTicket([new File(['x'], 'bilet.png', { type: 'image/png' })]))

    expect(error.status).toBe(502)
    expect(error.detail).toBe('AI nie odpowiada – wpisz dane ręcznie.')
  })

  it('maps 401 to ApiError with status 401', async () => {
    stubFetch(new Response(null, { status: 401 }))

    const error = await caught(api.listCases())

    expect(error.status).toBe(401)
    expect(error.detail.length).toBeGreaterThan(0)
  })

  it('maps validation errors to camelCase field errors', async () => {
    stubFetch(
      problem(400, {
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { FullName: ['Pole „Imię i nazwisko” zawiera niedozwolone znaki.'], '$.iban': ['Zły format.'] },
      }),
    )

    const error = await caught(
      api.updateProfile({ fullName: 'Jan 😀', address: null, iban: null, preferredPayout: 'OriginalPayment' }),
    )

    expect(error.status).toBe(400)
    expect(error.fieldErrors.fullName).toEqual(['Pole „Imię i nazwisko” zawiera niedozwolone znaki.'])
    expect(error.fieldErrors.iban).toEqual(['Zły format.'])
    expect(error.detail).toBe('Pole „Imię i nazwisko” zawiera niedozwolone znaki.')
  })

  it('sends cookies and JSON with every request', async () => {
    const fetchMock = stubFetch(new Response(JSON.stringify({ recovered: 10, pending: 5, totalCases: 2 }), { status: 200 }))

    const stats = await api.getStats()

    expect(stats.totalCases).toBe(2)
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/stats')
    expect(init.credentials).toBe('include')
  })

  it('logs in with cookies and translates a failed login to Polish', async () => {
    const fetchMock = stubFetch(problem(401, { status: 401, title: 'Unauthorized', detail: 'Failed' }))

    const error = await caught(api.login('a@b.pl', 'zle-haslo'))

    expect((fetchMock.mock.calls[0] as unknown as [string])[0]).toBe('/api/auth/login?useCookies=true')
    expect(error.status).toBe(401)
    expect(error.detail).toBe('Nieprawidłowy e-mail lub hasło.')
  })

  it('turns a network failure into ApiError with status 0', async () => {
    stubFetch(new TypeError('Failed to fetch'))

    const error = await caught(api.getReference())

    expect(error.status).toBe(0)
    expect(error.detail).toMatch(/połączenia/)
  })
})
