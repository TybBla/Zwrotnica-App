import { useEffect } from 'react'

export const SITE_TITLE = 'Zwrotnica – odszkodowanie za opóźniony pociąg'

/** The tab title names the page first ("Moje sprawy – Zwrotnica"), so history and screen readers can tell pages apart. */
export function usePageTitle(title: string | null) {
  useEffect(() => {
    document.title = title ? `${title} – Zwrotnica` : SITE_TITLE
  }, [title])
}
