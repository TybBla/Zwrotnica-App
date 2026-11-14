import type {
  AssessmentDto,
  AssessmentRequest,
  CaseDto,
  CaseRequest,
  CaseStatus,
  ComplaintDto,
  ComplaintPdfRequest,
  ComplaintRequest,
  ExtractionDto,
  FollowUpKind,
  ProfileDto,
  ReferenceDataDto,
  StatsDto,
  UpdateProfileRequest,
  UserInfo,
} from './types'

/** Field name (camelCase, as in the request body) → Polish messages from the server. */
export type FieldErrors = Record<string, string[]>

/** Every failed call throws this. `status` 0 means the server could not be reached. */
export class ApiError extends Error {
  readonly status: number
  /** Polish, ready to show to the user. */
  readonly detail: string
  readonly fieldErrors: FieldErrors

  constructor(status: number, detail: string, fieldErrors: FieldErrors = {}) {
    super(detail)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
    this.fieldErrors = fieldErrors
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

const fallbackMessages: Record<number, string> = {
  0: 'Brak połączenia z serwerem. Sprawdź internet i spróbuj ponownie.',
  401: 'Zaloguj się, aby kontynuować.',
  403: 'Nie masz dostępu do tego zasobu.',
  404: 'Nie znaleziono – mogło zostać usunięte.',
  429: 'Zbyt wiele zapytań. Spróbuj ponownie za chwilę.',
}

function fallbackMessage(status: number): string {
  return fallbackMessages[status] ?? (status >= 500
    ? 'Coś poszło nie tak po stronie serwera. Spróbuj ponownie za chwilę.'
    : 'Nie udało się wykonać operacji. Sprawdź dane i spróbuj ponownie.')
}

// ASP.NET reports "FullName" for model errors and "$.fullName" for JSON errors; the UI keys fields by camelCase.
function toFieldKey(key: string): string {
  const name = key.replace(/^\$\.?/, '')
  return name.charAt(0).toLowerCase() + name.slice(1)
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    const text = await response.text()
    return text ? (JSON.parse(text) as ProblemDetails) : {}
  } catch {
    return {}
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = await readProblem(response)
  const fieldErrors: FieldErrors = {}
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    fieldErrors[toFieldKey(key)] = messages
  }
  const firstFieldMessage = Object.values(fieldErrors).flat()[0]
  return new ApiError(response.status, problem.detail ?? firstFieldMessage ?? fallbackMessage(response.status), fieldErrors)
}

async function send(path: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, { ...init, credentials: 'include' })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, fallbackMessage(0))
  }
  if (!response.ok) throw await toApiError(response)
  return response
}

function jsonInit(method: string, body?: unknown): RequestInit {
  return body === undefined
    ? { method, headers: { Accept: 'application/json' } }
    : { method, headers: { Accept: 'application/json', 'Content-Type': 'application/json' }, body: JSON.stringify(body) }
}

async function json<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await send(path, { ...jsonInit(method, body), signal })
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** POSTs the letter text and resolves to the rendered PDF file. */
async function pdf(path: string, letter: ComplaintPdfRequest): Promise<Blob> {
  const response = await send(path, {
    method: 'POST',
    headers: { Accept: 'application/pdf', 'Content-Type': 'application/json' },
    body: JSON.stringify(letter),
  })
  return response.blob()
}

async function noContent(path: string, method: string, body?: unknown): Promise<void> {
  await send(path, jsonInit(method, body))
}

// ASP.NET Identity error codes → the form field they belong to and a Polish message. /register returns them as
// validation-problem keys ({"errors": {"PasswordTooShort": ["Passwords must be at least 6 characters."]}}).
const identityMessages: Record<string, { field: 'email' | 'password'; message: string }> = {
  DuplicateUserName: { field: 'email', message: 'Konto z tym adresem e-mail już istnieje. Zaloguj się.' },
  DuplicateEmail: { field: 'email', message: 'Konto z tym adresem e-mail już istnieje. Zaloguj się.' },
  InvalidEmail: { field: 'email', message: 'Podaj poprawny adres e-mail, np. jan@example.com.' },
  InvalidUserName: { field: 'email', message: 'Podaj poprawny adres e-mail, np. jan@example.com.' },
  PasswordTooShort: { field: 'password', message: 'Hasło musi mieć co najmniej 6 znaków.' },
  PasswordRequiresDigit: { field: 'password', message: 'Hasło musi zawierać cyfrę (0–9).' },
  PasswordRequiresLower: { field: 'password', message: 'Hasło musi zawierać małą literę (a–z).' },
  PasswordRequiresUpper: { field: 'password', message: 'Hasło musi zawierać wielką literę (A–Z).' },
  PasswordRequiresNonAlphanumeric: { field: 'password', message: 'Hasło musi zawierać znak specjalny, np. ! lub #.' },
  PasswordRequiresUniqueChars: { field: 'password', message: 'Hasło musi zawierać więcej różnych znaków.' },
}

/** Field errors keyed by camelCase field name, as the client stores them ("passwordTooShort"). */
const identityCode = (key: string) => key.charAt(0).toUpperCase() + key.slice(1)

/**
 * Identity's English messages never reach the user: known codes become Polish messages on their own field
 * (e-mail problems under E-mail, password rules under Hasło); an unknown code becomes a general Polish message.
 */
function translateIdentityError(error: ApiError): ApiError {
  const keys = Object.keys(error.fieldErrors)
  if (keys.length === 0) return error
  const fieldErrors: FieldErrors = {}
  let unknown = false
  for (const key of keys) {
    const known = identityMessages[identityCode(key)]
    if (!known) {
      unknown = true
      continue
    }
    const list = (fieldErrors[known.field] ??= [])
    if (!list.includes(known.message)) list.push(known.message)
  }
  const messages = Object.values(fieldErrors).flat()
  if (messages.length === 0 && unknown)
    return new ApiError(error.status, 'Nie udało się założyć konta. Sprawdź adres e-mail i hasło.')
  return new ApiError(error.status, messages.join(' '), fieldErrors)
}

async function withTranslatedErrors<T>(call: Promise<T>, translate: (error: ApiError) => ApiError): Promise<T> {
  try {
    return await call
  } catch (error) {
    throw error instanceof ApiError ? translate(error) : error
  }
}

export const api = {
  register: (email: string, password: string) =>
    withTranslatedErrors(noContent('/auth/register', 'POST', { email, password }), translateIdentityError),

  login: (email: string, password: string) =>
    withTranslatedErrors(noContent('/auth/login?useCookies=true', 'POST', { email, password }), (error) =>
      error.status === 401 ? new ApiError(401, 'Nieprawidłowy e-mail lub hasło.') : error,
    ),

  logout: () => noContent('/auth/logout', 'POST'),

  /** The signed-in user; throws ApiError 401 when nobody is signed in. */
  me: () => json<UserInfo>('/auth/manage/info'),

  getProfile: () => json<ProfileDto>('/profile'),
  updateProfile: (request: UpdateProfileRequest) => json<ProfileDto>('/profile', 'PUT', request),

  getReference: () => json<ReferenceDataDto>('/reference'),

  /** 409 = AI disabled, 502 = AI failed, 429 = rate limit, 400 = invalid files. Aborting rejects with an AbortError. */
  extractTicket: async (files: File[], signal?: AbortSignal): Promise<ExtractionDto> => {
    const form = new FormData()
    for (const file of files) form.append('files', file)
    const response = await send('/extractions', { method: 'POST', body: form, headers: { Accept: 'application/json' }, signal })
    return (await response.json()) as ExtractionDto
  },

  assess: (request: AssessmentRequest, signal?: AbortSignal) =>
    json<AssessmentDto>('/assessments', 'POST', request, signal),

  listCases: () => json<CaseDto[]>('/cases'),
  getCase: (id: number) => json<CaseDto>(`/cases/${id}`),
  createCase: (request: CaseRequest, signal?: AbortSignal) => json<CaseDto>('/cases', 'POST', request, signal),
  updateCase: (id: number, request: CaseRequest, signal?: AbortSignal) =>
    json<CaseDto>(`/cases/${id}`, 'PUT', request, signal),
  setStatus: (id: number, status: CaseStatus) => json<CaseDto>(`/cases/${id}/status`, 'PATCH', { status }),
  deleteCase: (id: number) => noContent(`/cases/${id}`, 'DELETE'),

  getStats: () => json<StatsDto>('/stats'),

  /** 409 when the case has no ticket price (nothing to describe). */
  generateComplaint: (id: number, request: ComplaintRequest, signal?: AbortSignal) =>
    json<ComplaintDto>(`/cases/${id}/complaint`, 'POST', request, signal),

  /** Renders the (possibly edited) letter; resolves to the PDF file. */
  downloadComplaintPdf: (id: number, letter: ComplaintPdfRequest) => pdf(`/cases/${id}/complaint/pdf`, letter),

  /** Ponaglenie or skarga do UTK from the template; 409 when the case status does not allow it yet. */
  generateFollowUp: (id: number, kind: FollowUpKind, signal?: AbortSignal) =>
    json<ComplaintDto>(`/cases/${id}/letters/${kind}`, 'POST', undefined, signal),

  downloadFollowUpPdf: (id: number, kind: FollowUpKind, letter: ComplaintPdfRequest) =>
    pdf(`/cases/${id}/letters/${kind}/pdf`, letter),
}

export type Api = typeof api
