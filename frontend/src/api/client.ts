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

async function json<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await send(path, jsonInit(method, body))
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function noContent(path: string, method: string, body?: unknown): Promise<void> {
  await send(path, jsonInit(method, body))
}

// Identity API error codes → Polish (register returns them as validation-problem keys).
const identityMessages: Record<string, string> = {
  DuplicateUserName: 'Konto z tym adresem e-mail już istnieje.',
  DuplicateEmail: 'Konto z tym adresem e-mail już istnieje.',
  InvalidEmail: 'Podaj poprawny adres e-mail.',
  InvalidUserName: 'Podaj poprawny adres e-mail.',
  PasswordTooShort: 'Hasło musi mieć co najmniej 6 znaków.',
  PasswordRequiresDigit: 'Hasło musi zawierać cyfrę.',
  PasswordRequiresLower: 'Hasło musi zawierać małą literę.',
  PasswordRequiresUpper: 'Hasło musi zawierać wielką literę.',
  PasswordRequiresNonAlphanumeric: 'Hasło musi zawierać znak specjalny (np. !).',
  PasswordRequiresUniqueChars: 'Hasło musi zawierać więcej różnych znaków.',
}

function translateIdentityError(error: ApiError): ApiError {
  const messages = Object.keys(error.fieldErrors)
    .map((key) => identityMessages[key.charAt(0).toUpperCase() + key.slice(1)])
    .filter((message): message is string => Boolean(message))
  if (messages.length === 0) return error
  const unique = [...new Set(messages)]
  const field = unique.some((m) => m.startsWith('Hasło')) ? 'password' : 'email'
  return new ApiError(error.status, unique.join(' '), { [field]: unique })
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

  /** 409 = AI disabled, 502 = AI failed, 429 = rate limit, 400 = invalid files. */
  extractTicket: async (files: File[]): Promise<ExtractionDto> => {
    const form = new FormData()
    for (const file of files) form.append('files', file)
    const response = await send('/extractions', { method: 'POST', body: form, headers: { Accept: 'application/json' } })
    return (await response.json()) as ExtractionDto
  },

  assess: (request: AssessmentRequest) => json<AssessmentDto>('/assessments', 'POST', request),

  listCases: () => json<CaseDto[]>('/cases'),
  getCase: (id: number) => json<CaseDto>(`/cases/${id}`),
  createCase: (request: CaseRequest) => json<CaseDto>('/cases', 'POST', request),
  updateCase: (id: number, request: CaseRequest) => json<CaseDto>(`/cases/${id}`, 'PUT', request),
  setStatus: (id: number, status: CaseStatus) => json<CaseDto>(`/cases/${id}/status`, 'PATCH', { status }),
  deleteCase: (id: number) => noContent(`/cases/${id}`, 'DELETE'),

  getStats: () => json<StatsDto>('/stats'),

  /** 409 when the case is not eligible for compensation. */
  generateComplaint: (id: number, request: ComplaintRequest) =>
    json<ComplaintDto>(`/cases/${id}/complaint`, 'POST', request),

  /** Renders the (possibly edited) letter; resolves to the PDF file. */
  downloadComplaintPdf: async (id: number, letter: ComplaintPdfRequest): Promise<Blob> => {
    const response = await send(`/cases/${id}/complaint/pdf`, {
      method: 'POST',
      headers: { Accept: 'application/pdf', 'Content-Type': 'application/json' },
      body: JSON.stringify(letter),
    })
    return response.blob()
  },
}

export type Api = typeof api
