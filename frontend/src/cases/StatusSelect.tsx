import { useId } from 'react'
import type { CaseStatus } from '../api/types'
import { Select } from '../components/Select'
import { statusLabels, statusOrder } from '../hooks/useCases'

const options = statusOrder.map((value) => ({ value, label: statusLabels[value] }))

interface StatusSelectProps {
  value: CaseStatus
  onChange: (status: CaseStatus) => void
  /** Id of the element naming the case, so each select reads e.g. "Status, PKP Intercity IC". */
  caseLabelId?: string
  disabled?: boolean
}

/** Native select (the OS picker on phones): Nowa / Wysłana / Wypłacona / Odrzucona. */
export function StatusSelect({ value, onChange, caseLabelId, disabled }: StatusSelectProps) {
  const id = useId()
  return (
    <div className="status-select">
      <label className="status-select__label" id={`${id}-label`} htmlFor={id}>
        Status
      </label>
      <Select
        id={id}
        value={value}
        onValueChange={onChange}
        options={options}
        disabled={disabled}
        aria-labelledby={[`${id}-label`, caseLabelId].filter(Boolean).join(' ')}
      />
    </div>
  )
}
