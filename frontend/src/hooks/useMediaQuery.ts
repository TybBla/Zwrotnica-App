import { useSyncExternalStore } from 'react'

const supported = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function'

/** Whether a media query matches, kept in sync (e.g. a tablet docked to a keyboard and trackpad). */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      if (!supported()) return () => {}
      const list = window.matchMedia(query)
      list.addEventListener('change', onChange)
      return () => list.removeEventListener('change', onChange)
    },
    () => supported() && window.matchMedia(query).matches,
    () => false,
  )
}
