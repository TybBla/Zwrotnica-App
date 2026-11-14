import { describe, expect, it } from 'vitest'
import { ACCEPT_ATTR, MAX_FILE_BYTES, addTicketFiles } from './ticketFiles'

const file = (name: string, type: string, bytes = 1024) => new File([new Uint8Array(bytes)], name, { type })

describe('addTicketFiles', () => {
  it('accepts images and PDF', () => {
    expect(ACCEPT_ATTR).toBe('image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf')
    const result = addTicketFiles([], [file('a.jpg', 'image/jpeg'), file('b.pdf', 'application/pdf')])
    expect(result.files.map((f) => f.name)).toEqual(['a.jpg', 'b.pdf'])
    expect(result.errors).toEqual([])
  })

  it('rejects other formats with a Polish message', () => {
    const result = addTicketFiles([], [file('notes.txt', 'text/plain')])
    expect(result.files).toEqual([])
    expect(result.errors[0]).toMatch(/nie jest obsługiwany/)
  })

  it('rejects files over 5 MB and empty files', () => {
    const result = addTicketFiles([], [file('big.png', 'image/png', MAX_FILE_BYTES + 1), file('empty.png', 'image/png', 0)])
    expect(result.files).toEqual([])
    expect(result.errors.join(' ')).toMatch(/big\.png.*5 MB/)
    expect(result.errors.join(' ')).toMatch(/empty\.png.*pusty/)
  })

  it('keeps at most 4 files in total', () => {
    const current = [file('1.jpg', 'image/jpeg'), file('2.jpg', 'image/jpeg'), file('3.jpg', 'image/jpeg')]
    const result = addTicketFiles(current, [file('4.jpg', 'image/jpeg'), file('5.jpg', 'image/jpeg')])
    expect(result.files).toHaveLength(4)
    expect(result.errors[0]).toMatch(/maksymalnie 4/)
  })

  it('recognises HEIC by extension when the browser gives no type, and sends it typed', () => {
    const result = addTicketFiles([], [file('IMG_0001.HEIC', '')])
    expect(result.errors).toEqual([])
    expect(result.files[0].type).toBe('image/heic')
  })
})
