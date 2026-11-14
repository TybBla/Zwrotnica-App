import { describe, expect, it } from 'vitest'
import { safeNext, signInPath } from './paths'

describe('safeNext', () => {
  it('follows same-site paths', () => {
    expect(safeNext('/sprawy/3?x=1')).toBe('/sprawy/3?x=1')
  })

  it('falls back for anything that could leave the site', () => {
    for (const next of [null, '', 'sprawy', 'https://evil.example', '//evil.example', '/\\evil.example']) {
      expect(safeNext(next)).toBe('/sprawy')
    }
  })

  it('rejects control characters (browsers strip tabs and line breaks, turning "/\t/host" into "//host")', () => {
    for (const next of ['/\t/evil.example', '/\n/evil.example', '/\r/evil.example', '/sprawy\u0000', '/\u007f']) {
      expect(safeNext(next)).toBe('/sprawy')
    }
  })

  it('builds the sign-in link with the way back encoded', () => {
    expect(signInPath('/sprawy/3')).toBe('/logowanie?next=%2Fsprawy%2F3')
  })
})
