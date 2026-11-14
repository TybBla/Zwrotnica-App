import { useAuth } from './useAuth'
import { CredentialsForm, type CredentialsFormProps } from './CredentialsForm'

export function LoginForm({ onSuccess, ...props }: CredentialsFormProps & { onSuccess?: () => void }) {
  const { login } = useAuth()
  return (
    <CredentialsForm
      {...props}
      submitLabel="Zaloguj się"
      passwordAutoComplete="current-password"
      onSubmit={async (email, password) => {
        await login(email, password)
        onSuccess?.()
      }}
    />
  )
}
