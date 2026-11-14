import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { ComplaintDto } from '../api/types'
import { Button } from '../components/Button'
import { AlertCircleIcon, CheckIcon, FileIcon } from '../components/icons'
import { copyText } from '../lib/clipboard'
import { saveBlob } from '../lib/download'

type Format = 'text' | 'pdf'
type Part = 'subject' | 'body'

const COPIED_MS = 2000

/** "[adres]"-style placeholders the passenger still has to fill in. */
const PLACEHOLDER = /(\[[^\]\n]{1,60}\])/

/** The letter body with line breaks kept and the placeholders marked. */
function LetterBody({ text }: { text: string }) {
  return (
    <p className="letter__body">
      {text.split(PLACEHOLDER).map((part, index) =>
        index % 2 === 1 ? (
          <mark key={index} className="letter__gap">
            {part}
          </mark>
        ) : (
          part
        ),
      )}
    </p>
  )
}

interface LetterChoiceProps {
  caseId: number
  letter: ComplaintDto
}

/** The finished letter: copy it as text (subject + body) or download the very same text as a PDF. */
export function LetterChoice({ caseId, letter }: LetterChoiceProps) {
  const id = useId()
  const [format, setFormat] = useState<Format>('text')
  const [copied, setCopied] = useState<Part | null>(null)
  const [copyFailed, setCopyFailed] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [downloadError, setDownloadError] = useState<string | null>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)
  const copiedTimer = useRef<number | undefined>(undefined)

  // The letter replaces the save form: bring focus (and the reader) to it.
  useLayoutEffect(() => {
    const heading = headingRef.current
    heading?.focus({ preventScroll: true })
    heading?.scrollIntoView?.({ block: 'start' })
  }, [])

  useEffect(() => () => window.clearTimeout(copiedTimer.current), [])

  const copy = async (part: Part) => {
    const ok = await copyText(part === 'subject' ? letter.subject : letter.body)
    setCopyFailed(!ok)
    setCopied(ok ? part : null)
    window.clearTimeout(copiedTimer.current)
    if (ok) copiedTimer.current = window.setTimeout(() => setCopied(null), COPIED_MS)
  }

  const download = async () => {
    setDownloading(true)
    setDownloadError(null)
    try {
      // Exactly the text on screen – the server only renders it.
      const blob = await api.downloadComplaintPdf(caseId, { subject: letter.subject, body: letter.body })
      saveBlob(blob, `reklamacja-${caseId}.pdf`)
    } catch {
      setDownloadError('Nie udało się przygotować pliku PDF. Spróbuj ponownie albo skopiuj tekst.')
    } finally {
      setDownloading(false)
    }
  }

  const copyButton = (part: Part, label: string) => (
    <Button
      variant="secondary"
      size="sm"
      className="letter__copy"
      iconStart={copied === part ? <CheckIcon size={16} /> : undefined}
      aria-label={copied === part ? undefined : label}
      onClick={() => void copy(part)}
    >
      {copied === part ? 'Skopiowano' : 'Kopiuj'}
    </Button>
  )

  const missing = letter.missingFields

  return (
    <section className="letter" aria-labelledby={`${id}-title`}>
      <header className="letter__head">
        <h3 className="letter__title" id={`${id}-title`} ref={headingRef} tabIndex={-1}>
          Pismo jest gotowe
        </h3>
        <p className="letter__lead">Sprawa jest zapisana na Twoim koncie. Wyślij pismo przewoźnikowi – e-mailem, przez jego formularz reklamacyjny albo w kasie.</p>
      </header>

      {missing.length > 0 && (
        <div className="notice letter__missing" role="status" aria-labelledby={`${id}-missing`}>
          <AlertCircleIcon className="notice__icon" size={20} />
          <div className="notice__body">
            <p className="notice__title" id={`${id}-missing`}>
              Uzupełnij w piśmie: {missing.join(', ')}
            </p>
            <p className="notice__text">Te miejsca są zaznaczone w treści nawiasami [ ]. Uzupełnij je przed wysłaniem.</p>
          </div>
        </div>
      )}

      <div className="letter__formats">
        <h4 className="letter__label" id={`${id}-format`}>
          Wybierz format
        </h4>
        <div className="segmented" role="radiogroup" aria-labelledby={`${id}-format`}>
          {(
            [
              ['text', 'Tekst'],
              ['pdf', 'PDF'],
            ] as const
          ).map(([value, label]) => (
            <label key={value} className="segmented__option" data-checked={format === value || undefined}>
              <input
                type="radio"
                className="segmented__input"
                name={`${id}-format`}
                checked={format === value}
                onChange={() => setFormat(value)}
              />
              {label}
            </label>
          ))}
        </div>
      </div>

      {format === 'pdf' && (
        <div className="letter__pdf wizard-swap">
          <FileIcon className="letter__pdf-icon" size={24} />
          <div className="letter__pdf-text">
            <p className="letter__pdf-name">reklamacja-{caseId}.pdf</p>
            <p className="letter__pdf-hint">Ta sama treść co poniżej – do wydruku lub jako załącznik do e-maila.</p>
          </div>
          <Button loading={downloading} onClick={() => void download()} className="letter__pdf-button">
            Pobierz PDF
          </Button>
          {downloadError && (
            <p className="inline-error letter__pdf-error" role="alert">
              <AlertCircleIcon size={16} />
              {downloadError}
            </p>
          )}
        </div>
      )}

      <article className="letter__doc" aria-label="Treść pisma">
        <div className="letter__part">
          <div className="letter__part-head">
            <h4 className="letter__label">Temat</h4>
            {format === 'text' && copyButton('subject', 'Kopiuj temat')}
          </div>
          <p className="letter__subject">{letter.subject}</p>
        </div>
        <div className="letter__part">
          <div className="letter__part-head">
            <h4 className="letter__label">Treść</h4>
            {format === 'text' && copyButton('body', 'Kopiuj treść')}
          </div>
          <LetterBody text={letter.body} />
        </div>
      </article>

      <p className="visually-hidden" aria-live="polite">
        {copied === 'subject' ? 'Temat skopiowany do schowka.' : copied === 'body' ? 'Treść skopiowana do schowka.' : ''}
      </p>
      {copyFailed && (
        <p className="inline-error" role="alert">
          <AlertCircleIcon size={16} />
          Nie udało się skopiować – zaznacz tekst i skopiuj go ręcznie.
        </p>
      )}
    </section>
  )
}
