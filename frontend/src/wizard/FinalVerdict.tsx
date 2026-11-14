import { Fragment, useId } from 'react'
import type { OutcomeDto } from '../api/types'
import type { BadgeTone } from '../components/Badge'
import './FinalVerdict.css'

interface FinalVerdictProps {
  outcome: OutcomeDto
  /** The verdict's signal colour (same as the result card). */
  tone: BadgeTone
  /** 3 inside the wizard step (under its h2), 2 as a section of the case page. */
  headingLevel?: 2 | 3
}

const AMOUNT = /(\d+(?:,\d{2})? zł)/

/** Amounts set in the same tabular figures as elsewhere; the rest of the server's sentence stays as written. */
function Statement({ text }: { text: string }) {
  return text.split(AMOUNT).map((part, index) =>
    index % 2 === 1 ? (
      <span key={index} className="tabular final-verdict__amount">
        {part}
      </span>
    ) : (
      <Fragment key={index}>{part}</Fragment>
    ),
  )
}

/** "Werdykt końcowy": what legal effect the complaint can be expected to have, as a formal statement block. */
export function FinalVerdict({ outcome, tone, headingLevel = 3 }: FinalVerdictProps) {
  const id = useId()
  const Heading = headingLevel === 2 ? 'h2' : 'h3'

  return (
    <section className="final-verdict" data-tone={tone} aria-labelledby={id}>
      <Heading className="final-verdict__heading" id={id}>
        Werdykt końcowy
      </Heading>
      <p className="final-verdict__title">{outcome.title}</p>
      <p className="final-verdict__statement">
        <Statement text={outcome.statement} />
      </p>
      {(outcome.legalBasis || outcome.deadline) && (
        <dl className="final-verdict__facts">
          {outcome.legalBasis && (
            <div>
              <dt>Podstawa prawna</dt>
              <dd>{outcome.legalBasis}</dd>
            </div>
          )}
          {outcome.deadline && (
            <div>
              <dt>Termin</dt>
              <dd>{outcome.deadline}</dd>
            </div>
          )}
        </dl>
      )}
      <p className="final-verdict__disclaimer">Ocena ma charakter informacyjny i nie stanowi porady prawnej.</p>
    </section>
  )
}
