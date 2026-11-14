export type Theme = 'light' | 'dark'

/** Same key as the inline script in index.html that sets the theme before first paint. */
export const THEME_STORAGE_KEY = 'refund-pilot-theme'

const darkQuery = '(prefers-color-scheme: dark)'

// Matches --header-bg of each scheme: colors the mobile browser chrome / status bar.
const themeColors: Record<Theme, string> = { light: '#163a70', dark: '#141415' }

export function systemTheme(): Theme {
  return typeof window.matchMedia === 'function' && window.matchMedia(darkQuery).matches ? 'dark' : 'light'
}

/** The user's explicit choice, or null when they follow the system. Storage may be unavailable (private mode). */
export function storedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

export function storeTheme(theme: Theme): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Not persisted; the toggle still works for this visit.
  }
}

export function currentTheme(): Theme {
  const attribute = document.documentElement.dataset.theme
  return attribute === 'light' || attribute === 'dark' ? attribute : storedTheme() ?? systemTheme()
}

export function applyTheme(theme: Theme, { animate = false } = {}): void {
  const root = document.documentElement
  if (animate) {
    root.classList.add('theme-transition')
    window.setTimeout(() => root.classList.remove('theme-transition'), 260)
  }
  root.dataset.theme = theme
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColors[theme])
}

/** Calls back when the OS scheme changes; returns an unsubscribe function. */
export function onSystemThemeChange(callback: (theme: Theme) => void): () => void {
  if (typeof window.matchMedia !== 'function') return () => {}
  const query = window.matchMedia(darkQuery)
  const listener = (event: MediaQueryListEvent) => callback(event.matches ? 'dark' : 'light')
  query.addEventListener('change', listener)
  return () => query.removeEventListener('change', listener)
}
