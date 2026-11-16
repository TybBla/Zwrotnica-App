import { useCallback, useEffect, useState } from 'react'
import { applyTheme, currentTheme, storeTheme, type Theme } from './theme'

/** Light/dark scheme: starts from the stored choice, defaulting to light. */
export function useTheme() {
  const [theme, setTheme] = useState<Theme>(currentTheme)

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  const toggle = useCallback(() => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark'
    storeTheme(next)
    applyTheme(next, { animate: true })
    setTheme(next)
  }, [theme])

  return { theme, toggle }
}
