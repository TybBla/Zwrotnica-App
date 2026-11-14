import { useEffect } from 'react'

export const SITE_TITLE = 'Refund Pilot – odszkodowanie za opóźniony pociąg'

/** The tab title names the page first ("Moje sprawy – Refund Pilot"), so history and screen readers can tell pages apart. */
export function usePageTitle(title: string | null) {
  useEffect(() => {
    document.title = title ? `${title} – Refund Pilot` : SITE_TITLE
  }, [title])
}
