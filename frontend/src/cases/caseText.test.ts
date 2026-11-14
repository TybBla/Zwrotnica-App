import { describe, expect, it } from 'vitest'
import { casesCount } from './caseText'

describe('casesCount', () => {
  it('uses the Polish plural forms', () => {
    expect(casesCount(0)).toBe('0 spraw')
    expect(casesCount(1)).toBe('1 sprawa')
    expect(casesCount(2)).toBe('2 sprawy')
    expect(casesCount(4)).toBe('4 sprawy')
    expect(casesCount(5)).toBe('5 spraw')
    expect(casesCount(12)).toBe('12 spraw')
    expect(casesCount(14)).toBe('14 spraw')
    expect(casesCount(22)).toBe('22 sprawy')
    expect(casesCount(25)).toBe('25 spraw')
    expect(casesCount(112)).toBe('112 spraw')
  })
})
