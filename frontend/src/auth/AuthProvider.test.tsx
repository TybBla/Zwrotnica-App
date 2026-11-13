import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './useAuth'

function Probe() {
  const { user, loading, login, logout } = useAuth()
  if (loading) return <p>sprawdzanie</p>
  return (
    <div>
      <p>{user ? `zalogowany: ${user.email}` : 'wylogowany'}</p>
      <button type="button" onClick={() => login('demo@refundpilot.pl', 'Demo123!')}>
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
          return Promise.resolve(signedIn ? json({ email: 'demo@refundpilot.pl', isEmailConfirmed: true }) : new Response(null, { status: 401 }))
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
    await waitFor(() => expect(screen.getByText('zalogowany: demo@refundpilot.pl')).toBeTruthy())

    await user.click(screen.getByRole('button', { name: 'logout' }))
    await waitFor(() => expect(screen.getByText('wylogowany')).toBeTruthy())
  })
})
