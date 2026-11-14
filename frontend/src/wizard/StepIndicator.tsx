import { CheckIcon } from '../components/icons'

const WIZARD_STEPS = ['Bilet', 'Opóźnienie', 'Wynik'] as const

/** Bilet → Opóźnienie → Wynik; finished steps show a check, the current one is marked for assistive tech. */
export function StepIndicator({ current }: { current: number }) {
  return (
    <ol className="steps-indicator" aria-label="Postęp">
      {WIZARD_STEPS.map((label, index) => {
        const state = index < current ? 'done' : index === current ? 'current' : 'todo'
        return (
          <li key={label} className="steps-indicator__item" data-state={state} aria-current={state === 'current' ? 'step' : undefined}>
            <span className="steps-indicator__dot" aria-hidden="true">
              {state === 'done' ? <CheckIcon size={14} strokeWidth={2.5} /> : index + 1}
            </span>
            <span className="steps-indicator__label">{label}</span>
            {state === 'done' && <span className="visually-hidden"> (gotowe)</span>}
          </li>
        )
      })}
    </ol>
  )
}
