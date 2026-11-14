import { describe, expect, it } from 'vitest'
import { STATIONS } from './stations'
import { isAllowed } from './textRules'

describe('STATIONS', () => {
  it('lists major stations under their official names, with diacritics', () => {
    expect(STATIONS.length).toBeGreaterThanOrEqual(100)
    for (const name of ['Warszawa Centralna', 'Kraków Główny', 'Łódź Fabryczna', 'Gdańsk Wrzeszcz', 'Bielsko-Biała Główna'])
      expect(STATIONS).toContain(name)
  })

  it('has no duplicates and every name passes the station field rules', () => {
    expect(new Set(STATIONS).size).toBe(STATIONS.length)
    for (const name of STATIONS) expect(isAllowed('station', name)).toBe(true)
  })
})
