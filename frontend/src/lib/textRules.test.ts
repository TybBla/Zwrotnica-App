import { describe, expect, it } from 'vitest'
import { canonicalize, filterAllowed, finalizeText, isAllowed, isValidIban } from './textRules'

describe('canonicalize', () => {
  it('maps smart punctuation to plain characters', () => {
    expect(canonicalize('O’Neil – „test” …')).toBe('O\'Neil - "test" ...')
  })

  it('turns tabs, unicode spaces and line breaks into single spaces on one-line fields', () => {
    expect(canonicalize('Kraków \tGłówny\r\nPeron  2')).toBe('Kraków Główny Peron 2')
  })

  it('keeps line breaks in multiline mode', () => {
    expect(canonicalize('Linia 1\r\nLinia 2 Linia 3', { multiline: true })).toBe('Linia 1\nLinia 2\nLinia 3')
  })

  it('composes decomposed letters (NFC)', () => {
    expect(canonicalize('Wrocław Główny')).toBe('Wrocław Główny')
  })

  it('does not trim while typing, so a space between words can be entered', () => {
    expect(canonicalize('Jan ')).toBe('Jan ')
  })
})

describe('filterAllowed', () => {
  it('strips emoji and symbols from a name', () => {
    expect(filterAllowed('fullName', 'Jan 😀 Kowalski™')).toBe('Jan Kowalski')
  })

  it('canonicalizes apostrophes before filtering', () => {
    expect(filterAllowed('fullName', 'O’Neil')).toBe("O'Neil")
  })

  it('keeps Polish station names', () => {
    expect(filterAllowed('station', 'Kraków Główny')).toBe('Kraków Główny')
  })

  it('drops digits from a name but keeps them in an address', () => {
    expect(filterAllowed('fullName', 'Anna 2')).toBe('Anna ')
    expect(filterAllowed('address', 'ul. Długa 5/12, 00-001 Łódź')).toBe('ul. Długa 5/12, 00-001 Łódź')
  })

  it('allows only letters, digits and spaces in a train category', () => {
    expect(filterAllowed('trainCategory', 'EIP-1 (ok)')).toBe('EIP1 ok')
  })

  it('keeps notes punctuation and line breaks', () => {
    expect(filterAllowed('notes', 'Spóźnienie 75 min!\nPociąg „IC” stał 50% czasu 🙁')).toBe(
      'Spóźnienie 75 min!\nPociąg "IC" stał 50% czasu ',
    )
  })

  it('upper-cases and keeps only ASCII letters, digits and spaces in an IBAN', () => {
    expect(filterAllowed('iban', 'pl61 1090-1014 ą')).toBe('PL61 10901014 ')
  })
})

describe('finalizeText', () => {
  it('trims and collapses on blur', () => {
    expect(finalizeText('station', '  Warszawa   Centralna ')).toBe('Warszawa Centralna')
  })

  it('removes spaces around line breaks in notes', () => {
    expect(finalizeText('notes', 'a \n b ')).toBe('a\nb')
  })
})

describe('isAllowed', () => {
  it('accepts allowed text and rejects symbols', () => {
    expect(isAllowed('carrier', 'PKP Intercity')).toBe(true)
    expect(isAllowed('carrier', 'PKP Intercity ★')).toBe(false)
  })
})

describe('isValidIban', () => {
  it('accepts a Polish IBAN with or without the country code', () => {
    expect(isValidIban('PL61 1090 1014 0000 0712 1981 2874')).toBe(true)
    expect(isValidIban('61109010140000071219812874')).toBe(true)
  })

  it('rejects a wrong checksum', () => {
    expect(isValidIban('PL61 1090 1014 0000 0712 1981 2875')).toBe(false)
  })
})
