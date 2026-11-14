/** Sign-in page link that brings the user back to `path` afterwards. */
export const signInPath = (path: string, page: '/logowanie' | '/rejestracja' = '/logowanie') =>
  `${page}?next=${encodeURIComponent(path)}`

/**
 * Only a same-site path is followed after signing in ("/sprawy/3"), never "//host" or "https://…"
 * (an open redirect would let a crafted link send the user elsewhere right after they typed their password).
 */
export function safeNext(next: string | null, fallback = '/sprawy'): string {
  // Browsers read "/\host" like "//host", so any backslash is refused too.
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.includes('\\')) return fallback
  return next
}
