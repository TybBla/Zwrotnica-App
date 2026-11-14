import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from './api/client'
import App from './App'
import { AuthProvider } from './auth/AuthProvider'

let signedIn = false

function Where() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname + location.search}</output>
}

const renderAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
        <Where />
      </AuthProvider>
    </MemoryRouter>,
  )

beforeEach(() => {
  signedIn = false
  // jsdom has no layout: the route change scrolls to the top.
  vi.stubGlobal('scrollTo', () => {})
  vi.spyOn(api, 'me').mockImplementation(async () => {
    if (!signedIn) throw new ApiError(401, 'Zaloguj się.')
    return { email: 'jan@example.com', isEmailConfirmed: true }
  })
  vi.spyOn(api, 'login').mockImplementation(async () => {
    signedIn = true
  })
  vi.spyOn(api, 'listCases').mockResolvedValue([])
  vi.spyOn(api, 'getStats').mockResolvedValue({ recovered: 0, pending: 0, totalCases: 0 })
  vi.spyOn(api, 'getReference').mockResolvedValue({ carriers: [], categories: [] })
})

describe('routing', () => {
  it('sends a guest from a protected page to sign-in and back after signing in', async () => {
    const user = userEvent.setup()
    renderAt('/sprawy')
    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/logowanie?next=%2Fsprawy'))
    const heading = screen.getByRole('heading', { level: 1, name: 'Zaloguj się' })
    expect(heading).toBeTruthy()

    await user.type(screen.getByLabelText('E-mail'), 'jan@example.com')
    await user.type(screen.getByLabelText('Hasło'), 'Haslo123!')
    await user.click(within(screen.getByRole('main')).getByRole('button', { name: 'Zaloguj się' }))

    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/sprawy'))
    expect(await screen.findByRole('heading', { level: 1, name: 'Moje sprawy' })).toBeTruthy()
  })

  it('never follows a "next" that leaves the site', async () => {
    signedIn = true
    renderAt('/logowanie?next=//evil.example')
    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/sprawy'))
  })

  it('opens the registration tab on /rejestracja', async () => {
    renderAt('/rejestracja')
    expect(await screen.findByRole('heading', { level: 1, name: 'Załóż konto' })).toBeTruthy()
    expect(screen.getByRole('tab', { name: 'Rejestracja' }).getAttribute('aria-selected')).toBe('true')
  })

  it('starts the wizard from the home page on /sprawdz and moves focus to the new page heading', async () => {
    const user = userEvent.setup()
    renderAt('/')
    await user.click(await screen.findByRole('button', { name: /Wpisz ręcznie/ }))
    expect(screen.getByTestId('location').textContent).toBe('/sprawdz?bilet=reczne')
    await waitFor(() => expect(document.activeElement?.tagName === 'H1').toBe(true))
  })

  it('offers "Moje sprawy" and "Profil" in the account menu', async () => {
    signedIn = true
    const user = userEvent.setup()
    renderAt('/')
    await user.click(await screen.findByRole('button', { name: 'Konto: jan@example.com' }))
    await user.click(screen.getByRole('button', { name: 'Profil' }))
    expect(screen.getByTestId('location').textContent).toBe('/konto')
  })
})
