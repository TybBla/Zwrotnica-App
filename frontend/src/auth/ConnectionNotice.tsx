import { Button } from '../components/Button'
import { WarningIcon } from '../components/icons'
import { useAuth } from './useAuth'
import './ConnectionNotice.css'

/**
 * A slim, non-blocking line under the header when the session check could not reach the API.
 * Nothing is signed out: the guest wizard keeps working, and "Spróbuj ponownie" repeats the check.
 */
export function ConnectionNotice() {
  const { offline, retrying, retry } = useAuth()
  return (
    // The live region stays mounted, so the message is announced when it appears.
    <div className="connection-notice" role="status">
      {offline && (
        <div className="container container--wide connection-notice__bar">
          <WarningIcon size={18} className="connection-notice__icon" />
          <p className="connection-notice__text">
            <strong>Brak połączenia z serwerem.</strong> Nie wiemy, czy jesteś zalogowany. Sprawdzenie biletu działa dalej.
          </p>
          <Button variant="secondary" size="sm" loading={retrying} onClick={retry} className="connection-notice__retry">
            Spróbuj ponownie
          </Button>
        </div>
      )}
    </div>
  )
}
