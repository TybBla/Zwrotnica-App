import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Button } from './Button'

describe('Button loading', () => {
  it('keeps focus while loading and ignores presses', async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    function Saving() {
      const [busy, setBusy] = useState(false)
      return (
        <Button
          loading={busy}
          onClick={() => {
            onClick()
            setBusy(true)
          }}
        >
          Zapisz
        </Button>
      )
    }
    render(<Saving />)
    const button = screen.getByRole<HTMLButtonElement>('button', { name: 'Zapisz' })

    await user.click(button)
    expect(onClick).toHaveBeenCalledTimes(1)
    // Not `disabled`: a disabled button would lose focus to <body> in the middle of the save.
    expect(button.disabled).toBe(false)
    expect(document.activeElement).toBe(button)
    expect(button.getAttribute('aria-disabled')).toBe('true')
    expect(button.getAttribute('aria-busy')).toBe('true')

    await user.click(button)
    await user.keyboard('{Enter}')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('does not submit its form while loading', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault())
    render(
      <form onSubmit={onSubmit}>
        <input aria-label="E-mail" />
        <Button type="submit" loading>
          Zaloguj się
        </Button>
      </form>,
    )

    await user.click(screen.getByRole('button', { name: 'Zaloguj się' }))
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'a{Enter}')
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
