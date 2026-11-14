import { useEffect, useId, useState } from 'react'
import { api } from '../api/client'
import type { ReferenceDataDto } from '../api/types'
import { UTK_URL } from '../cases/DeadlineBadge'
import { ExternalLinkIcon } from '../components/icons'

export const GENERIC_SEND_ADVICE =
  'Wyślij pismo przez formularz reklamacyjny na oficjalnej stronie przewoźnika, e-mailem na adres podany w jego regulaminie, pocztą albo złóż je w kasie biletowej. Zachowaj potwierdzenie wysłania.'

const UTK_FALLBACK = 'Aktualny adres i sposoby kontaktu znajdziesz na stronie Urzędu Transportu Kolejowego. Zachowaj potwierdzenie wysłania.'

interface Link {
  href: string
  label: string
}

/** "https://www.intercity.pl/pl/…" → "intercity.pl": the address the passenger should see in the browser bar. */
const siteOf = (href: string) => new URL(href).hostname.replace(/^www\./, '')

interface WhereToSendProps {
  carrier: string
  /** A reminder goes to the carrier; a complaint to the regulator goes to UTK. */
  recipient: 'carrier' | 'utk'
}

/** Official channels for the finished letter. Links only from the server's verified list; always the generic advice. */
export function WhereToSend({ carrier, recipient }: WhereToSendProps) {
  const id = useId()
  const [reference, setReference] = useState<ReferenceDataDto | null>(null)

  useEffect(() => {
    let active = true
    api
      .getReference()
      .then((data) => active && setReference(data))
      .catch(() => {}) // the generic advice is shown anyway
    return () => {
      active = false
    }
  }, [])

  let channels: string | null = null
  const links: Link[] = []
  if (recipient === 'utk') {
    const utk = reference?.utk
    channels = utk?.complaintChannels ?? null
    const href = utk?.complaintUrl ?? UTK_URL
    links.push({ href, label: `Urząd Transportu Kolejowego: ${siteOf(href)}` })
  } else {
    const match = reference?.carriers.find((c) => c.name.toLocaleLowerCase('pl') === carrier.trim().toLocaleLowerCase('pl'))
    channels = match?.complaintChannels ?? null
    if (match?.complaintUrl) links.push({ href: match.complaintUrl, label: `Strona przewoźnika: ${siteOf(match.complaintUrl)}` })
    if (match?.complaintFormUrl)
      links.push({ href: match.complaintFormUrl, label: `Formularz reklamacyjny (PDF) – ${siteOf(match.complaintFormUrl)}` })
  }

  return (
    <section className="send-to" aria-labelledby={`${id}-title`}>
      <h4 className="letter__label" id={`${id}-title`}>
        {recipient === 'utk' ? 'Gdzie wysłać skargę' : 'Gdzie wysłać pismo'}
      </h4>
      {channels && <p className="send-to__channels">{channels}</p>}
      {links.length > 0 && (
        <ul className="send-to__links">
          {links.map((link) => (
            <li key={link.href}>
              <a className="send-to__link" href={link.href} target="_blank" rel="noopener noreferrer">
                {link.label}
                <ExternalLinkIcon size={16} />
                <span className="visually-hidden"> (otwiera się w nowej karcie)</span>
              </a>
            </li>
          ))}
        </ul>
      )}
      <p className="send-to__advice">{recipient === 'utk' ? UTK_FALLBACK : GENERIC_SEND_ADVICE}</p>
    </section>
  )
}
