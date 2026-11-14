import { useId, type Ref } from 'react'
import type { PayoutPreference } from '../api/types'
import { Checkbox } from '../components/Checkbox'
import { Field } from '../components/Field'
import { TextInput } from '../components/TextInput'
import './PayoutChoice.css'

const payoutOptions: { value: PayoutPreference; title: string; description: string }[] = [
  { value: 'OriginalPayment', title: 'Zwrot na konto/kartę użyte do zakupu', description: 'Najprościej – przewoźnik odda pieniądze tam, skąd przyszły.' },
  { value: 'BankTransfer', title: 'Przelew na inny rachunek', description: 'Podasz numer rachunku, na który mają trafić pieniądze.' },
  { value: 'Voucher', title: 'Bon', description: 'Do wykorzystania na kolejne bilety tego przewoźnika.' },
]

export const IBAN_NOTE =
  'Numer jest szyfrowany w bazie, widzisz go tylko Ty i nigdy nie trafia do AI. Możesz go też pominąć – zostawimy w piśmie miejsce do uzupełnienia.'

interface PayoutRadiosProps {
  value: PayoutPreference
  onChange: (value: PayoutPreference) => void
  legend: string
  hint?: string
  disabled?: boolean
}

/** The three payout forms as native radios drawn as rows (≥44 px), in a labelled radiogroup. */
export function PayoutRadios({ value, onChange, legend, hint, disabled }: PayoutRadiosProps) {
  const id = useId()
  return (
    <div
      className="payout__group"
      role="radiogroup"
      aria-labelledby={`${id}-legend`}
      aria-describedby={hint ? `${id}-hint` : undefined}
    >
      <h3 className="payout__legend" id={`${id}-legend`}>
        {legend}
      </h3>
      <div className="payout__options">
        {payoutOptions.map((option) => (
          <label key={option.value} className="payout-option" data-checked={value === option.value || undefined}>
            <input
              type="radio"
              className="payout-option__input"
              name={`${id}-payout`}
              value={option.value}
              checked={value === option.value}
              disabled={disabled}
              onChange={() => onChange(option.value)}
              aria-labelledby={`${id}-${option.value}-title`}
              aria-describedby={`${id}-${option.value}-description`}
            />
            <span className="payout-option__dot" aria-hidden="true" />
            <span className="payout-option__text">
              <span className="payout-option__title" id={`${id}-${option.value}-title`}>
                {option.title}
              </span>
              <span className="payout-option__description" id={`${id}-${option.value}-description`}>
                {option.description}
              </span>
            </span>
          </label>
        ))}
      </div>
      {hint && (
        <p className="payout__hint" id={`${id}-hint`}>
          {hint}
        </p>
      )}
    </div>
  )
}

interface PayoutChoiceProps {
  value: PayoutPreference
  onChange: (value: PayoutPreference) => void
  iban: string
  onIbanChange: (iban: string) => void
  onIbanBlur?: () => void
  ibanError?: string | null
  ibanRef?: Ref<HTMLInputElement>
  remember: boolean
  onRememberChange: (remember: boolean) => void
  /** The account number is already the one saved on the account – nothing to remember. */
  ibanSaved: boolean
  disabled?: boolean
  /** Defaults to the plain question; a complaint without a guaranteed amount asks it conditionally. */
  legend?: string
}

/** How the money should come back: three radio cards; the account number appears only for a transfer. */
export function PayoutChoice(props: PayoutChoiceProps) {
  const { value, onChange, iban, onIbanChange, onIbanBlur, ibanError, ibanRef, remember, onRememberChange, ibanSaved, disabled } = props
  const legend = props.legend ?? 'Jak chcesz otrzymać pieniądze?'

  return (
    <div className="payout">
      <PayoutRadios
        value={value}
        onChange={onChange}
        legend={legend}
        hint="Kupiłeś bilet gotówką w kasie? Wybierz przelew."
        disabled={disabled}
      />

      {value === 'BankTransfer' && (
        <div className="payout__iban wizard-swap">
          <Field label="Numer rachunku" optional tooltip={IBAN_NOTE} error={ibanError} hint="Polski numer możesz wpisać bez „PL”.">
            <TextInput
              ref={ibanRef}
              field="iban"
              maxLength={50}
              value={iban}
              onValueChange={onIbanChange}
              onBlur={onIbanBlur}
              disabled={disabled}
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              placeholder="PL00 0000 0000 0000 0000 0000 0000"
              className="mono"
            />
          </Field>
          {ibanSaved ? (
            <p className="payout__saved">To numer zapisany na Twoim koncie.</p>
          ) : (
            <Checkbox
              label="Zapamiętaj na koncie"
              description="Następnym razem nie trzeba będzie go wpisywać."
              checked={remember}
              disabled={disabled}
              onCheckedChange={onRememberChange}
            />
          )}
        </div>
      )}
    </div>
  )
}
