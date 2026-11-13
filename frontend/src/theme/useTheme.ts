import { useCallback, useEffect, useState } from 'react'
import { applyTheme, currentTheme, onSystemThemeChange, storedTheme, storeTheme, type Theme } from './theme'

/** Light/dark scheme: starts from the stored choice or the OS setting and follows the OS until the user picks one. */
export function useTheme() {
  const [theme, setTheme] = useState<Theme>(currentTheme)

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  useEffect(
    () =>
      onSystemThemeChange((system) => {
        if (storedTheme() === null) {
          applyTheme(system, { animate: true })
          setTheme(system)
        }
      }),
    [],
  )

  const toggle = useCallback(() => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark'
    storeTheme(next)
    applyTheme(next, { animate: true })
    setTheme(next)
  }, [theme])

  return { theme, toggle }
}
