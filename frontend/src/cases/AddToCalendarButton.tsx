import { Button } from '../components/Button'
import { CalendarIcon } from '../components/icons'
import { deadlineCalendar, type DeadlineEventInput } from '../lib/calendar'
import { saveBlob } from '../lib/download'

type AddToCalendarButtonProps = Omit<DeadlineEventInput, 'caseUrl' | 'now'>

/** Downloads the carrier's response deadline as an .ics file (built here, nothing goes to the server). */
export function AddToCalendarButton(props: AddToCalendarButtonProps) {
  const download = () => {
    const { content, fileName } = deadlineCalendar({ ...props, caseUrl: `${window.location.origin}/sprawy/${props.caseId}` })
    saveBlob(new Blob([content], { type: 'text/calendar;charset=utf-8' }), fileName)
  }

  return (
    <Button variant="secondary" size="sm" iconStart={<CalendarIcon size={16} />} onClick={download}>
      Dodaj do kalendarza
    </Button>
  )
}
