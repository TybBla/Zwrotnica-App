import { afterEach, describe, expect, it, vi } from 'vitest'
import { copyText } from './clipboard'

const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard')

afterEach(() => {
  if (original) Object.defineProperty(navigator, 'clipboard', original)
  else Reflect.deleteProperty(navigator, 'clipboard')
  Reflect.deleteProperty(document, 'execCommand')
})

describe('copyText fallback', () => {
  it('selects the whole text (iOS needs setSelectionRange), copies and gives focus back', async () => {
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true })
    let copied = ''
    Object.defineProperty(document, 'execCommand', {
      configurable: true,
      value: vi.fn(() => {
        const area = document.activeElement as HTMLTextAreaElement
        copied = area.value.slice(area.selectionStart, area.selectionEnd)
        return true
      }),
    })
    const button = document.createElement('button')
    document.body.append(button)
    button.focus()

    expect(await copyText('Szanowni Państwo,\nproszę o zwrot.')).toBe(true)
    expect(copied).toBe('Szanowni Państwo,\nproszę o zwrot.')
    expect(document.activeElement).toBe(button)
    expect(document.querySelector('textarea')).toBeNull()
    button.remove()
  })
})
