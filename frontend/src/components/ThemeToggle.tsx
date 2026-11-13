import { useTheme } from '../theme/useTheme'
import { MoonIcon, SunIcon } from './icons'
import './ThemeToggle.css'

/** Switches between the light and dark color scheme; the choice is remembered on this device. */
export function ThemeToggle() {
  const { theme, toggle } = useTheme()
  const isDark = theme === 'dark'

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={toggle}
      aria-label={isDark ? 'Włącz jasny motyw' : 'Włącz ciemny motyw'}
      title={isDark ? 'Jasny motyw' : 'Ciemny motyw'}
      data-theme-state={theme}
    >
      <span className="theme-toggle__icon theme-toggle__icon--sun">
        <SunIcon />
      </span>
      <span className="theme-toggle__icon theme-toggle__icon--moon">
        <MoonIcon />
      </span>
    </button>
  )
}
