import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import type { PayoutPreference, ProfileDto, UpdateProfileRequest } from '../api/types'
import { Button } from '../components/Button'
import { Field } from '../components/Field'
import { AlertCircleIcon, CheckCircleIcon } from '../components/icons'
import { TextInput } from '../components/TextInput'
import { finalizeText, isValidIban, normalizeIban } from '../lib/textRules'
import { usePageTitle } from '../routes/usePageTitle'
import { PayoutRadios } from '../wizard/PayoutChoice'
import './Profile.css'

export const PROFILE_IBAN_NOTE =
  'Numer jest szyfrowany w bazie, widzisz go tylko Ty i nigdy nie trafia do AI. Jest potrzebny tylko przy przelewie na inny rachunek.'

const INVALID_IBAN = 'Sprawdź numer rachunku – wygląda na niepoprawny. Możesz zostawić to pole puste.'

interface Form {
  fullName: string
  address: string
  iban: string
  preferredPayout: PayoutPreference
}

type FieldName = keyof Form
type Errors = Partial<Record<FieldName | 'form', string>>

const toForm = (profile: ProfileDto): Form => ({
  fullName: profile.fullName ?? '',
  address: profile.address ?? '',
  iban: profile.iban ?? '',
  preferredPayout: profile.preferredPayout,
})

const orNull = (value: string) => (value === '' ? null : value)

function toRequest(form: Form): UpdateProfileRequest {
  const iban = finalizeText('iban', form.iban)
  return {
    fullName: orNull(finalizeText('fullName', form.fullName)),
    address: orNull(finalizeText('address', form.address)),
    iban: iban === '' ? null : normalizeIban(iban),
    preferredPayout: form.preferredPayout,
  }
}

const fieldNames: FieldName[] = ['fullName', 'address', 'iban', 'preferredPayout']

/** ProblemDetails field errors → one Polish message per field; anything else is a form-level message. */
function toErrors(error: unknown): Errors {
  if (!(error instanceof ApiError)) return { form: 'Nie udało się zapisać zmian. Spróbuj ponownie.' }
  const errors: Errors = {}
  for (const name of fieldNames) {
    const messages = error.fieldErrors[name]
    if (messages?.length) errors[name] = messages.join(' ')
  }
  if (Object.keys(errors).length === 0) errors.form = error.detail
  return errors
}

type Load = { state: 'loading' } | { state: 'error'; message: string } | { state: 'ready'; email: string }

/** /konto – the data that goes into letters. Everything is optional except the e-mail, which cannot be changed here. */
export function ProfilePage() {
  usePageTitle('Profil')
  const [load, setLoad] = useState<Load>({ state: 'loading' })
  const [form, setForm] = useState<Form>({ fullName: '', address: '', iban: '', preferredPayout: 'OriginalPayment' })
  const [errors, setErrors] = useState<Errors>({})
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const fullNameRef = useRef<HTMLInputElement>(null)
  const addressRef = useRef<HTMLInputElement>(null)
  const ibanRef = useRef<HTMLInputElement>(null)
  const formErrorRef = useRef<HTMLDivElement>(null)

  const fetchProfile = useCallback((isActive: () => boolean) => {
    api
      .getProfile()
      .then((profile) => {
        if (!isActive()) return
        setForm(toForm(profile))
        setLoad({ state: 'ready', email: profile.email })
      })
      .catch((error: unknown) => {
        if (isActive()) setLoad({ state: 'error', message: error instanceof ApiError ? error.detail : 'Nie udało się wczytać profilu.' })
      })
  }, [])

  useEffect(() => {
    let active = true
    fetchProfile(() => active)
    return () => {
      active = false
    }
  }, [fetchProfile])

  const retry = () => {
    setLoad({ state: 'loading' })
    fetchProfile(() => true)
  }

  const update = (patch: Partial<Form>) => {
    setForm((current) => ({ ...current, ...patch }))
    setSaved(false)
    const touched = Object.keys(patch) as FieldName[]
    setErrors((current) => {
      const next = { ...current }
      for (const name of touched) delete next[name]
      return next
    })
  }

  const checkIban = () => {
    const iban = finalizeText('iban', form.iban)
    setErrors((current) => ({ ...current, iban: iban !== '' && !isValidIban(iban) ? INVALID_IBAN : undefined }))
  }

  const focusFirstError = (found: Errors) => {
    const first = ([
      [found.fullName, fullNameRef],
      [found.address, addressRef],
      [found.iban, ibanRef],
    ] as const).find(([message]) => message)?.[1]
    if (first) first.current?.focus()
    else if (found.form || found.preferredPayout) formErrorRef.current?.focus()
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (saving) return
    setSaved(false)
    const iban = finalizeText('iban', form.iban)
    if (iban !== '' && !isValidIban(iban)) {
      const found = { iban: INVALID_IBAN }
      setErrors(found)
      focusFirstError(found)
      return
    }
    setErrors({})
    setSaving(true)
    try {
      const profile = await api.updateProfile(toRequest(form))
      setForm(toForm(profile))
      setSaved(true)
    } catch (error) {
      const found = toErrors(error)
      setErrors(found)
      focusFirstError(found)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="container container--wide page page--column profile-page">
      <header className="page__head">
        <h1 className="page__title" tabIndex={-1}>
          Profil
        </h1>
        <p className="page__lead">
          Te dane wstawimy do pisma, żeby nie wpisywać ich za każdym razem. Wszystkie są opcjonalne – brakujące zostaną w
          piśmie jako miejsce do uzupełnienia.
        </p>
      </header>

      {load.state === 'loading' && (
        <p className="page__loading" role="status">
          Wczytywanie profilu…
        </p>
      )}

      {load.state === 'error' && (
        <div className="page-error" role="alert">
          <AlertCircleIcon className="page-error__icon" />
          <div className="page-error__body">
            <p className="page-error__title">Nie udało się wczytać profilu</p>
            <p>{load.message}</p>
            <Button variant="secondary" size="sm" onClick={retry}>
              Spróbuj ponownie
            </Button>
          </div>
        </div>
      )}

      {load.state === 'ready' && (
        <form className="profile-form" onSubmit={(event) => void submit(event)} noValidate>
          <Field label="E-mail" hint="Adres konta – nie można go tu zmienić.">
            <TextInput value={load.email} onValueChange={() => {}} readOnly type="email" autoComplete="email" />
          </Field>

          <Field label="Imię i nazwisko" optional error={errors.fullName}>
            <TextInput
              ref={fullNameRef}
              field="fullName"
              value={form.fullName}
              onValueChange={(fullName) => update({ fullName })}
              autoComplete="name"
              maxLength={200}
            />
          </Field>

          <Field label="Adres" optional error={errors.address} hint="Do korespondencji, np. ul. Długa 1, 00-001 Warszawa.">
            <TextInput
              ref={addressRef}
              field="address"
              value={form.address}
              onValueChange={(address) => update({ address })}
              autoComplete="street-address"
              maxLength={300}
            />
          </Field>

          <Field label="Numer rachunku" optional tooltip={PROFILE_IBAN_NOTE} error={errors.iban} hint="Polski numer możesz wpisać bez „PL”.">
            <TextInput
              ref={ibanRef}
              field="iban"
              value={form.iban}
              onValueChange={(iban) => update({ iban })}
              onBlur={checkIban}
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              inputMode="text"
              placeholder="PL00 0000 0000 0000 0000 0000 0000"
              className="mono"
            />
          </Field>

          <div className="profile-form__payout">
            <PayoutRadios
              value={form.preferredPayout}
              onChange={(preferredPayout) => update({ preferredPayout })}
              legend="Preferowana forma wypłaty"
              hint="Zaznaczymy ją w kolejnych sprawach; w każdej możesz wybrać inną."
              disabled={saving}
            />
            {errors.preferredPayout && (
              <p className="field__error" role="alert">
                <AlertCircleIcon size={16} />
                {errors.preferredPayout}
              </p>
            )}
          </div>

          {errors.form && (
            <div className="page-error" role="alert" tabIndex={-1} ref={formErrorRef}>
              <AlertCircleIcon className="page-error__icon" />
              <div className="page-error__body">
                <p>{errors.form}</p>
              </div>
            </div>
          )}

          <div className="profile-form__actions">
            <Button type="submit" loading={saving}>
              Zapisz zmiany
            </Button>
            <p className="profile-form__saved" role="status">
              {saved && (
                <>
                  <CheckCircleIcon size={18} />
                  Zapisano zmiany.
                </>
              )}
            </p>
          </div>
        </form>
      )}
    </div>
  )
}
