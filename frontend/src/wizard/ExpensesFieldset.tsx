import { useState } from 'react'
import { Field } from '../components/Field'
import { TextArea, TextInput } from '../components/TextInput'
import { expenseIssue, type ExpensesInput } from '../hooks/useWizard'

interface ExpensesFieldsetProps {
  value: ExpensesInput
  onChange: (patch: Partial<ExpensesInput>) => void
}

/**
 * Regional carriers are outside the EU 25%/50% scheme but usually refund proven damage: the costs the passenger
 * incurred (taxi, overnight stay…). Optional; with an amount the letter becomes a claim for those costs.
 */
export function ExpensesFieldset({ value, onChange }: ExpensesFieldsetProps) {
  const [amountTouched, setAmountTouched] = useState(false)
  const error = amountTouched ? expenseIssue(value.amount) : null

  return (
    <fieldset className="expenses">
      <legend className="expenses__legend">Poniesione koszty (opcjonalnie)</legend>
      <p className="expenses__hint">Zachowaj rachunki – trzeba je dołączyć do wniosku.</p>
      <Field label="Opis kosztów">
        <TextArea
          field="expenseDescription"
          maxLength={500}
          rows={3}
          value={value.description}
          onValueChange={(description) => onChange({ description })}
          placeholder="np. taksówka do Szczecina po odwołaniu pociągu"
        />
      </Field>
      <Field label="Kwota kosztów" error={error}>
        <TextInput
          value={value.amount}
          onValueChange={(amount) => onChange({ amount: amount.replace(/[^\d.,]/g, '') })}
          onBlur={() => setAmountTouched(true)}
          inputMode="decimal"
          enterKeyHint="done"
          placeholder="np. 85,00"
          autoComplete="off"
          suffix="zł"
          className="tabular delay-form__minutes"
        />
      </Field>
    </fieldset>
  )
}
