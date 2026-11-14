import { useId, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { Button } from '../components/Button'
import { Field } from '../components/Field'
import { AlertCircleIcon } from '../components/icons'
import { TextInput } from '../components/TextInput'

export interface CredentialsFormProps {
  /** Shared between the two tabs, so switching tabs keeps what was typed. */
  email: string
  onEmailChange: (email: string) => void
  /** Focus the e-mail field when the form appears (dialog). */
  autoFocus?: boolean
}

interface Props extends CredentialsFormProps {
  submitLabel: string
  passwordAutoComplete: 'current-password' | 'new-password'
  passwordHint?: string
  /** Throws ApiError (Polish detail / fieldErrors) on failure. */
  onSubmit: (email: string, password: string) => Promise<void>
}

interface Errors {
  email?: string
  password?: string
  form?: string
}

const looksLikeEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)

/** E-mail + password with Polish validation; the server's field errors land on their fields. */
export function CredentialsForm({ email, onEmailChange, autoFocus, submitLabel, passwordAutoComplete, passwordHint, onSubmit }: Props) {
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [errors, setErrors] = useState<Errors>({})
  const [busy, setBusy] = useState(false)
  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)
  const formErrorId = useId()

  const validate = (): Errors => {
    const trimmed = email.trim()
    const next: Errors = {}
    if (trimmed === '') next.email = 'Podaj adres e-mail.'
    else if (!looksLikeEmail(trimmed)) next.email = 'Podaj poprawny adres e-mail, np. jan@example.com.'
    if (password === '') next.password = 'Podaj hasło.'
    return next
  }

  const focusFirst = (found: Errors) => {
    if (found.email) emailRef.current?.focus()
    else if (found.password) passwordRef.current?.focus()
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (busy) return
    const found = validate()
    setErrors(found)
    if (found.email || found.password) {
      focusFirst(found)
      return
    }
    setBusy(true)
    try {
      await onSubmit(email.trim(), password)
    } catch (error) {
      const api = error instanceof ApiError ? error : null
      const next: Errors = {
        email: api?.fieldErrors.email?.join(' '),
        password: api?.fieldErrors.password?.join(' '),
      }
      if (!next.email && !next.password) next.form = api?.detail ?? 'Nie udało się. Spróbuj ponownie.'
      setErrors(next)
      focusFirst(next)
      setBusy(false)
    }
    // On success the panel goes away (or the caller moves on); nothing to reset here.
  }

  return (
    <form className="auth-form" noValidate onSubmit={submit} aria-describedby={errors.form ? formErrorId : undefined}>
      <Field label="E-mail" error={errors.email}>
        <TextInput
          ref={emailRef}
          type="email"
          value={email}
          onValueChange={onEmailChange}
          autoComplete={passwordAutoComplete === 'new-password' ? 'email' : 'username'}
          inputMode="email"
          autoCapitalize="none"
          spellCheck={false}
          enterKeyHint="next"
          autoFocus={autoFocus}
        />
      </Field>
      <Field label="Hasło" error={errors.password} hint={passwordHint}>
        <TextInput
          ref={passwordRef}
          type={showPassword ? 'text' : 'password'}
          value={password}
          onValueChange={setPassword}
          autoComplete={passwordAutoComplete}
          autoCapitalize="none"
          spellCheck={false}
          enterKeyHint="go"
        />
      </Field>
      <button type="button" className="link-button auth-form__reveal" aria-pressed={showPassword} onClick={() => setShowPassword((v) => !v)}>
        {showPassword ? 'Ukryj hasło' : 'Pokaż hasło'}
      </button>
      {errors.form && (
        <p className="inline-error" id={formErrorId} role="alert">
          <AlertCircleIcon size={16} />
          {errors.form}
        </p>
      )}
      <Button type="submit" block loading={busy}>
        {submitLabel}
      </Button>
    </form>
  )
}
