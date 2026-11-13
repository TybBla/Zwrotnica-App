import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { Field } from './Field'
import { TextInput } from './TextInput'

function NameField() {
  const [value, setValue] = useState('')
  return (
    <Field label="Imię i nazwisko" hint="Tak jak na bilecie" tooltip="Potrzebne do pisma reklamacyjnego.">
      <TextInput field="fullName" value={value} onValueChange={setValue} />
    </Field>
  )
}

describe('TextInput with a character allowlist', () => {
  it('never shows disallowed characters while typing', async () => {
    const user = userEvent.setup()
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko') as HTMLInputElement

    await user.type(input, 'Jan 😀 Kowalski')

    expect(input.value).toBe('Jan Kowalski')
  })

  it('filters pasted text and canonicalizes apostrophes', async () => {
    const user = userEvent.setup()
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko') as HTMLInputElement

    await user.click(input)
    await user.paste('Seán O’Neil ★')

    expect(input.value).toBe("Seán O'Neil ")
  })

  it('leaves a dead-key / IME composition alone and filters when it ends', () => {
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko') as HTMLInputElement

    fireEvent.compositionStart(input)
    fireEvent.change(input, { target: { value: 'Józef Gr´' } })
    expect(input.value).toBe('Józef Gr´')

    fireEvent.change(input, { target: { value: 'Józef Gró' } })
    fireEvent.compositionEnd(input)
    expect(input.value).toBe('Józef Gró')
  })

  it('filters disallowed characters committed by a composition', () => {
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko') as HTMLInputElement

    fireEvent.compositionStart(input)
    fireEvent.change(input, { target: { value: 'Ola ★' } })
    fireEvent.compositionEnd(input)

    expect(input.value).toBe('Ola ')
  })

  it('trims on blur', async () => {
    const user = userEvent.setup()
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko') as HTMLInputElement

    await user.type(input, 'Anna Nowak ')
    expect(input.value).toBe('Anna Nowak ')
    await user.tab()

    expect(input.value).toBe('Anna Nowak')
  })

  it('links the hint to the input', () => {
    render(<NameField />)
    const input = screen.getByLabelText('Imię i nazwisko')
    const describedBy = input.getAttribute('aria-describedby') ?? ''
    const hint = screen.getByText('Tak jak na bilecie')

    expect(describedBy.split(' ')).toContain(hint.id)
  })
})
