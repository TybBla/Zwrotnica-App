import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AuthProvider } from './AuthProvider'
import { ConnectionNotice } from './ConnectionNotice'
import { useAuth } from './useAuth'

function Probe() {
  const { user, loading, login, logout } = useAuth()
  if (loading) return <p>sprawdzanie</p>
  return (
    <div>
      <p>{user ? `zalogowany: ${user.email}` : 'wylogowany'}</p>
      <button type="button" onClick={() => login('demo@zwrotnica.test', 'Demo123!')}>
        login
      </button>
      <button type="button" onClick={() => logout()}>
        logout
      </button>
    </div>
  )
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('AuthProvider', () => {
  it('starts signed out on 401, signs in and out', async () => {
    let signedIn = false
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => {
        if (url === '/api/auth/manage/info')
          return Promise.resolve(signedIn ? json({ email: 'demo@zwrotnica.test', isEmailConfirmed: true }) : new Response(null, { status: 401 }))
        if (url.startsWith('/api/auth/login')) {
          signedIn = true
          return Promise.resolve(new Response(null, { status: 200 }))
        }
        if (url === '/api/auth/logout') {
          signedIn = false
          return Promise.resolve(new Response(null, { status: 204 }))
        }
        return Promise.reject(new Error(`unexpected ${url}`))
      }),
    )
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    expect(await screen.findByText('wylogowany')).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'login' }))
    await waitFor(() => expect(screen.getByText('zalogowany: demo@zwrotnica.test')).toBeTruthy())

    await user.click(screen.getByRole('button', { name: 'logout' }))
    await waitFor(() => expect(screen.getByText('wylogowany')).toBeTruthy())
  })
})

function OfflineProbe() {
  const { user, loading, offline } = useAuth()
  if (loading) return <p>sprawdzanie</p>
  return <p>{offline ? 'offline' : user ? `zalogowany: ${user.email}` : 'wylogowany'}</p>
}

describe('AuthProvider when the API is unreachable', () => {
  it('does not treat a network error as signed out, and recovers on "Spróbuj ponownie"', async () => {
    let reachable = false
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => {
        if (url !== '/api/auth/manage/info') return Promise.reject(new Error(`unexpected ${url}`))
        return reachable
          ? Promise.resolve(json({ email: 'demo@zwrotnica.test', isEmailConfirmed: true }))
          : Promise.reject(new TypeError('Failed to fetch'))
      }),
    )
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <ConnectionNotice />
        <OfflineProbe />
      </AuthProvider>,
    )

    expect(await screen.findByText('offline')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Brak połączenia z serwerem.')

    reachable = true
    await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }))
    expect(await screen.findByText('zalogowany: demo@zwrotnica.test')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toBe('')
  })

  it('treats a 5xx on the session check as offline, and a 401 as signed out', async () => {
    let status = 503
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(null, { status }))),
    )
    const user = userEvent.setup()
    render(
      <AuthProvider>
        <ConnectionNotice />
        <OfflineProbe />
      </AuthProvider>,
    )

    expect(await screen.findByText('offline')).toBeTruthy()

    status = 401
    await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }))
    expect(await screen.findByText('wylogowany')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Spróbuj ponownie' })).toBeNull()
  })

  it('checks again by itself when the browser comes back online', async () => {
    let reachable = false
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        reachable
          ? Promise.resolve(json({ email: 'demo@zwrotnica.test', isEmailConfirmed: true }))
          : Promise.reject(new TypeError('Failed to fetch')),
      ),
    )
    render(
      <AuthProvider>
        <OfflineProbe />
      </AuthProvider>,
    )
    expect(await screen.findByText('offline')).toBeTruthy()

    reachable = true
    window.dispatchEvent(new Event('online'))
    expect(await screen.findByText('zalogowany: demo@zwrotnica.test')).toBeTruthy()
  })
})
