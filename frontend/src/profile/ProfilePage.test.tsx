import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { ProfileDto } from '../api/types'
import { ProfilePage } from './ProfilePage'

const profile: ProfileDto = {
  email: 'jan@example.com',
  fullName: 'Jan Kowalski',
  address: null,
  iban: null,
  preferredPayout: 'OriginalPayment',
  emailReminders: true,
}

beforeEach(() => {
  vi.spyOn(api, 'getProfile').mockResolvedValue(profile)
})

describe('ProfilePage', () => {
  it('shows the e-mail read-only and saves the edited profile', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'updateProfile').mockImplementation(async (request) => ({ email: profile.email, ...request }))
    render(<ProfilePage />)

    const name = (await screen.findByLabelText(/Imię i nazwisko/)) as HTMLInputElement
    expect(name.value).toBe('Jan Kowalski')
    const email = screen.getByLabelText('E-mail') as HTMLInputElement
    expect(email.readOnly).toBe(true)
    expect(email.value).toBe('jan@example.com')
    expect((screen.getByRole('radio', { name: /Zwrot na konto\/kartę/ }) as HTMLInputElement).checked).toBe(true)

    await user.type(screen.getByLabelText(/Adres/), 'ul. Długa 1, 00-001 Warszawa')
    await user.type(screen.getByRole('textbox', { name: /Numer rachunku/ }), '61 1090 1014 0000 0712 1981 2874')
    await user.click(screen.getByRole('radio', { name: /Przelew/ }))
    await user.click(screen.getByRole('button', { name: 'Zapisz zmiany' }))

    await waitFor(() =>
      expect(api.updateProfile).toHaveBeenCalledWith({
        fullName: 'Jan Kowalski',
        address: 'ul. Długa 1, 00-001 Warszawa',
        iban: 'PL61109010140000071219812874',
        preferredPayout: 'BankTransfer',
        emailReminders: true,
      }),
    )
    expect((await screen.findByRole('status')).textContent).toMatch(/Zapisano/)
  })

  it('shows the server validation errors on their fields', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'updateProfile').mockRejectedValue(
      new ApiError(400, 'Imię i nazwisko może mieć maksymalnie 200 znaków.', {
        fullName: ['Imię i nazwisko może mieć maksymalnie 200 znaków.'],
        address: ['Adres może mieć maksymalnie 300 znaków.'],
      }),
    )
    render(<ProfilePage />)
    await screen.findByDisplayValue('Jan Kowalski')
    await user.click(screen.getByRole('button', { name: 'Zapisz zmiany' }))

    const name = screen.getByLabelText(/Imię i nazwisko/)
    await waitFor(() => expect(name.getAttribute('aria-invalid')).toBe('true'))
    expect(document.getElementById(name.getAttribute('aria-describedby')!.split(' ')[0])?.textContent).toMatch(
      /maksymalnie 200 znaków/,
    )
    expect(screen.getByLabelText(/Adres/).getAttribute('aria-invalid')).toBe('true')
    expect(screen.getByText('Adres może mieć maksymalnie 300 znaków.')).toBeTruthy()
  })

  it('checks the account number before sending', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'updateProfile').mockResolvedValue(profile)
    render(<ProfilePage />)
    await user.type(await screen.findByRole('textbox', { name: /Numer rachunku/ }), 'PL00 1234')
    await user.click(screen.getByRole('button', { name: 'Zapisz zmiany' }))
    expect(await screen.findByText(/Sprawdź numer rachunku/)).toBeTruthy()
    expect(api.updateProfile).not.toHaveBeenCalled()
  })

  it('turns the reminder e-mails off and saves that choice', async () => {
    const user = userEvent.setup()
    vi.spyOn(api, 'updateProfile').mockImplementation(async (request) => ({ email: profile.email, ...request }))
    render(<ProfilePage />)

    const reminders = (await screen.findByRole('switch', { name: 'Przypomnienia e-mail' })) as HTMLInputElement
    expect(reminders.checked).toBe(true)
    expect(screen.getByText(/Miesiąc po wysłaniu pisma/)).toBeTruthy()

    await user.click(reminders)
    await user.click(screen.getByRole('button', { name: 'Zapisz zmiany' }))

    await waitFor(() => expect(api.updateProfile).toHaveBeenCalledWith(expect.objectContaining({ emailReminders: false })))
    expect(reminders.checked).toBe(false)
  })
})
