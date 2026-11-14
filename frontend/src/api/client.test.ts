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
      api.updateProfile({ fullName: 'Jan 😀', address: null, iban: null, preferredPayout: 'OriginalPayment', emailReminders: true }),
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

describe('register: ASP.NET Identity errors in Polish', () => {
  // The exact shape /api/auth/register returned for a weak password and a bad e-mail (checked against the API).
  const identityProblem = (errors: Record<string, string[]>) =>
    problem(400, { title: 'One or more validation errors occurred.', status: 400, errors })

  it('puts every broken password rule on the password field, in Polish', async () => {
    stubFetch(
      identityProblem({
        PasswordTooShort: ['Passwords must be at least 6 characters.'],
        PasswordRequiresNonAlphanumeric: ['Passwords must have at least one non alphanumeric character.'],
        PasswordRequiresDigit: ["Passwords must have at least one digit ('0'-'9')."],
        PasswordRequiresUpper: ["Passwords must have at least one uppercase ('A'-'Z')."],
        PasswordRequiresLower: ["Passwords must have at least one lowercase ('a'-'z')."],
      }),
    )

    const error = await caught(api.register('jan@example.com', 'abc'))

    expect(error.fieldErrors).toEqual({
      password: [
        'Hasło musi mieć co najmniej 6 znaków.',
        'Hasło musi zawierać znak specjalny, np. ! lub #.',
        'Hasło musi zawierać cyfrę (0–9).',
        'Hasło musi zawierać wielką literę (A–Z).',
        'Hasło musi zawierać małą literę (a–z).',
      ],
    })
    expect(error.detail).not.toMatch(/Passwords/)
  })

  it('puts a taken or invalid e-mail on the e-mail field, once', async () => {
    stubFetch(
      identityProblem({
        DuplicateUserName: ["Username 'jan@example.com' is already taken."],
        DuplicateEmail: ["Email 'jan@example.com' is already taken."],
      }),
    )
    const taken = await caught(api.register('jan@example.com', 'Haslo1!'))
    expect(taken.fieldErrors).toEqual({ email: ['Konto z tym adresem e-mail już istnieje. Zaloguj się.'] })

    stubFetch(identityProblem({ InvalidEmail: ["Email 'jan' is invalid."] }))
    const invalid = await caught(api.register('jan', 'Haslo1!'))
    expect(invalid.fieldErrors).toEqual({ email: ['Podaj poprawny adres e-mail, np. jan@example.com.'] })
  })

  it('splits mixed e-mail and password errors between the two fields', async () => {
    stubFetch(identityProblem({ InvalidEmail: ['x'], PasswordTooShort: ['y'] }))

    const error = await caught(api.register('jan', 'a'))

    expect(error.fieldErrors).toEqual({
      email: ['Podaj poprawny adres e-mail, np. jan@example.com.'],
      password: ['Hasło musi mieć co najmniej 6 znaków.'],
    })
  })

  it('never shows an unknown Identity code in English', async () => {
    stubFetch(identityProblem({ SomeFutureIdentityCode: ['Something went wrong in English.'] }))

    const error = await caught(api.register('jan@example.com', 'Haslo1!'))

    expect(error.detail).toBe('Nie udało się założyć konta. Sprawdź adres e-mail i hasło.')
    expect(error.fieldErrors).toEqual({})
  })
})
