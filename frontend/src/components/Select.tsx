import type { Ref, SelectHTMLAttributes } from 'react'
import { useFieldControl } from './fieldContext'
import { ChevronDownIcon } from './icons'
import './TextInput.css'

export interface SelectOption<V extends string = string> {
  value: V
  label: string
}

type SelectProps<V extends string> = Omit<SelectHTMLAttributes<HTMLSelectElement>, 'value' | 'onChange'> & {
  value: V | ''
  onValueChange: (value: V) => void
  options: readonly SelectOption<V>[]
  /** Shown first while nothing is chosen. */
  placeholder?: string
  invalid?: boolean
  ref?: Ref<HTMLSelectElement>
}

/** Native select (best on phones: the OS picker), styled like TextInput. */
export function Select<V extends string>({
  value,
  onValueChange,
  options,
  placeholder,
  invalid,
  className,
  id,
  ...props
}: SelectProps<V>) {
  const control = useFieldControl()
  const isInvalid = invalid ?? control?.invalid ?? false

  return (
    <div className={['input', 'input--select', className].filter(Boolean).join(' ')}>
      <select
        {...props}
        id={id ?? control?.id}
        value={value}
        onChange={(event) => onValueChange(event.target.value as V)}
        aria-invalid={isInvalid || undefined}
        aria-describedby={[props['aria-describedby'], control?.describedBy].filter(Boolean).join(' ') || undefined}
        required={props.required ?? control?.required}
        className="input__control"
        data-placeholder={value === '' || undefined}
      >
        {placeholder !== undefined && (
          <option value="" disabled>
            {placeholder}
          </option>
        )}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <ChevronDownIcon className="input__chevron" size={18} />
    </div>
  )
}
