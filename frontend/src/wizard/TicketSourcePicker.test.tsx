import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { TicketSourcePicker } from './TicketSourcePicker'

const ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf'

function stubPointer(coarse: boolean) {
  vi.stubGlobal(
    'matchMedia',
    (query: string) =>
      ({
        matches: query.includes('coarse') ? coarse : false,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  )
}

describe('TicketSourcePicker', () => {
  it('on touch devices offers a camera input and a gallery/file input', () => {
    stubPointer(true)
    const { container } = render(<TicketSourcePicker files={[]} onFilesChange={() => {}} />)

    expect(screen.getByRole('button', { name: /Zrób zdjęcie/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Wybierz z galerii lub pliku/ })).toBeTruthy()

    const inputs = [...container.querySelectorAll<HTMLInputElement>('input[type="file"]')]
    expect(inputs).toHaveLength(2)
    const camera = inputs.filter((input) => input.getAttribute('capture') === 'environment')
    const gallery = inputs.filter((input) => !input.hasAttribute('capture'))
    expect(camera).toHaveLength(1)
    expect(gallery).toHaveLength(1)
    for (const input of inputs) expect(input.accept).toBe(ACCEPT)
  })

  it('on desktop offers one file input with a drop zone', () => {
    stubPointer(false)
    const { container } = render(<TicketSourcePicker files={[]} onFilesChange={() => {}} />)

    expect(screen.getByRole('button', { name: /Wybierz plik/ })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Zrób zdjęcie/ })).toBeNull()
    expect(screen.getByText(/Przeciągnij/)).toBeTruthy()

    const inputs = [...container.querySelectorAll<HTMLInputElement>('input[type="file"]')]
    expect(inputs).toHaveLength(1)
    expect(inputs[0].hasAttribute('capture')).toBe(false)
    expect(inputs[0].accept).toBe(ACCEPT)
    expect(inputs[0].multiple).toBe(true)
  })

  it('previews a PDF as an icon with its name', () => {
    stubPointer(false)
    const pdf = new File([new Uint8Array(10)], 'e-bilet.pdf', { type: 'application/pdf' })
    render(<TicketSourcePicker files={[pdf]} onFilesChange={() => {}} />)

    expect(screen.getByText('e-bilet.pdf')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Usuń plik e-bilet.pdf' })).toBeTruthy()
  })
})
