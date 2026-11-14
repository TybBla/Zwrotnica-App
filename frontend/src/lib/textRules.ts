/**
 * Per-field character allowlists. Mirrors backend `TextRules` / `Iban` (RefundPilot.Application.Validation):
 * the UI drops disallowed characters while typing or pasting, the API rejects them with a 400.
 */
export type TextField =
  | 'fullName'
  | 'address'
  | 'carrier'
  | 'station'
  | 'trainCategory'
  | 'ticketNumber'
  | 'iban'
  | 'notes'
  | 'expenseDescription'

// Character-class bodies, same as the backend's (`\p{L}` letters of any script, `\p{N}` digits).
const classBodies: Record<TextField, string> = {
  fullName: String.raw`\p{L} \-'.`,
  address: String.raw`\p{L}\p{N} .,\-/'`,
  carrier: String.raw`\p{L}\p{N} .\-'()/`,
  station: String.raw`\p{L}\p{N} .\-'()/`,
  trainCategory: String.raw`\p{L}\p{N} `,
  ticketNumber: String.raw`\p{L}\p{N} \-/`,
  iban: String.raw`A-Za-z0-9 `,
  notes: String.raw`\p{L}\p{N} \n.,;:!?\-()/'"%`,
  expenseDescription: String.raw`\p{L}\p{N} \n.,;:!?\-()/'"%`,
}

// .NET regexes work on UTF-16 code units: a code point above U+FFFF is a surrogate pair (category Cs) and never
// matches \p{L} or \p{N} there. JS with the `u` flag sees the code point itself, so "𝐀𝐧𝐧𝐚" (fancy-font letters)
// would pass here and fail with a 400 on the server. Exclude the astral planes explicitly for parity.
const ASTRAL = String.raw`\u{10000}-\u{10FFFF}`

const disallowed = Object.fromEntries(
  Object.entries(classBodies).map(([field, body]) => [field, new RegExp(`[^${body}]|[${ASTRAL}]`, 'gu')]),
) as Record<TextField, RegExp>

const fullMatch = Object.fromEntries(
  Object.entries(classBodies).map(([field, body]) => [field, new RegExp(`^(?:(?![${ASTRAL}])[${body}])+$`, 'u')]),
) as Record<TextField, RegExp>

const isMultiline = (field: TextField) => field === 'notes' || field === 'expenseDescription'

const replacements: Record<string, string> = {
  '’': "'", '‘': "'", 'ʼ': "'",
  '–': '-', '—': '-', '−': '-',
  '“': '"', '”': '"', '„': '"',
  '…': '...',
}

/**
 * Maps what phone keyboards produce to the plain characters the allowlists accept: smart quotes/dashes, ellipsis,
 * tabs and any Unicode space separator → space, line breaks → "\n" (multiline) or a space. Collapses runs of spaces
 * and NFC-composes. Does not trim, so the user can still type the space between two words.
 */
export function canonicalize(value: string, { multiline = false } = {}): string {
  const text = value
    .replace(/\r\n|[\r\n\p{Zl}\p{Zp}]/gu, multiline ? '\n' : ' ')
    .replace(/[’‘ʼ–—−“”„…]/g, (c) => replacements[c])
    .replace(/[\t\p{Zs}]/gu, ' ')
    .normalize('NFC')
  return text.replace(/ {2,}/g, ' ')
}

/** Canonicalizes, then removes every character outside the field's allowlist. Used on each input and paste. */
export function filterAllowed(field: TextField, value: string): string {
  const canonical = canonicalize(value, { multiline: isMultiline(field) })
  const cased = field === 'iban' ? canonical.toUpperCase() : canonical
  return cased.replace(disallowed[field], '').replace(/ {2,}/g, ' ')
}

/** The value as it is stored: filtered and trimmed (spaces around line breaks dropped in notes). Used on blur/submit. */
export function finalizeText(field: TextField, value: string): string {
  const filtered = filterAllowed(field, value)
  const tidy = isMultiline(field) ? filtered.replace(/ ?\n ?/g, '\n') : filtered
  return tidy.trim()
}

export function isAllowed(field: TextField, value: string): boolean {
  return fullMatch[field].test(value.normalize('NFC'))
}

/** Removes spaces, upper-cases and prefixes a bare 26-digit Polish number with "PL" (backend `Iban.Normalize`). */
export function normalizeIban(value: string): string {
  const compact = value.replace(/ /g, '').toUpperCase()
  return /^\d{26}$/.test(compact) ? `PL${compact}` : compact
}

/** ISO 13616: 15–34 characters, country code + check digits, mod-97 remainder of 1. */
export function isValidIban(value: string): boolean {
  if (!/^[A-Za-z0-9 ]+$/.test(value)) return false
  const iban = normalizeIban(value)
  if (iban.length < 15 || iban.length > 34 || !/^[A-Z]{2}\d{2}/.test(iban)) return false

  let remainder = 0
  for (const c of iban.slice(4) + iban.slice(0, 4)) {
    remainder = /\d/.test(c)
      ? (remainder * 10 + Number(c)) % 97
      : (remainder * 100 + (c.charCodeAt(0) - 55)) % 97
  }
  return remainder === 1
}
