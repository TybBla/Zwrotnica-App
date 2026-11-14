import { useAuth } from './useAuth'
import { CredentialsForm, type CredentialsFormProps } from './CredentialsForm'

/** Mirrors the ASP.NET Identity defaults the API uses. */
const PASSWORD_HINT = 'Co najmniej 6 znaków, w tym wielka i mała litera, cyfra i znak specjalny (np. !).'

export function RegisterForm({ onSuccess, ...props }: CredentialsFormProps & { onSuccess?: () => void }) {
  const { register } = useAuth()
  return (
    <CredentialsForm
      {...props}
      submitLabel="Załóż konto"
      passwordAutoComplete="new-password"
      passwordHint={PASSWORD_HINT}
      onSubmit={async (email, password) => {
        await register(email, password)
        onSuccess?.()
      }}
    />
  )
}
