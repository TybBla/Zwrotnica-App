import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import { AccountMenu } from '../components/AccountMenu'
import { AuthDialog } from './AuthDialog'
import { AuthProvider } from './AuthProvider'

let signedIn = false

function Header() {
  const [open, setOpen] = useState(false)
  return (
    <>
      <AccountMenu onSignIn={() => setOpen(true)} />
      <AuthDialog open={open} onClose={() => setOpen(false)} />
    </>
  )
}

beforeEach(() => {
  signedIn = false
  vi.spyOn(api, 'me').mockImplementation(async () => {
    if (!signedIn) throw new ApiError(401, 'Zaloguj się.')
    return { email: 'jan@example.com', isEmailConfirmed: true }
  })
  vi.spyOn(api, 'login').mockImplementation(async () => {
    signedIn = true
  })
})

describe('AuthDialog', () => {
  it('opens from the header button as a labelled dialog and closes on Escape, returning focus', async () => {
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <Header />
      </AuthProvider>,
    )
    const trigger = await screen.findByRole('button', { name: 'Zaloguj się' })
    await user.click(trigger)
    const dialog = screen.getByRole('dialog', { name: 'Zaloguj się' })
    expect(dialog.contains(document.activeElement)).toBe(true)

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(document.activeElement).toBe(trigger)
  })

  it('keeps Tab inside the dialog', async () => {
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <Header />
      </AuthProvider>,
    )
    await user.click(await screen.findByRole('button', { name: 'Zaloguj się' }))
    const dialog = screen.getByRole('dialog')
    for (let i = 0; i < 8; i += 1) {
      await user.tab()
      expect(dialog.contains(document.activeElement)).toBe(true)
    }
  })

  it('signs in and closes', async () => {
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <Header />
      </AuthProvider>,
    )
    await user.click(await screen.findByRole('button', { name: 'Zaloguj się' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('E-mail'), 'jan@example.com')
    await user.type(within(dialog).getByLabelText('Hasło'), 'Haslo123!')
    await user.click(within(dialog).getByRole('button', { name: 'Zaloguj się' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    // The 'Zaloguj się' trigger is gone: focus lands on the account menu that replaced it.
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Konto: jan@example.com' })))
  })
})
