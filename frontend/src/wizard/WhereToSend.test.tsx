import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from '../api/client'
import type { ReferenceDataDto } from '../api/types'
import { GENERIC_SEND_ADVICE, WhereToSend } from './WhereToSend'

const reference: ReferenceDataDto = {
  carriers: [
    {
      name: 'PKP Intercity',
      coverage: 'Eligible',
      categories: ['IC'],
      complaintUrl: 'https://www.intercity.pl/pl/site/dla-pasazera/informacje/reklamacje-i-skargi/info.html',
      complaintFormUrl: 'https://www.intercity.pl/pl/dokumenty/formularze/Formularz.pdf',
      complaintChannels: 'Formularz online albo pocztą.',
    },
    { name: 'Koleje Mazowieckie', coverage: 'NotCovered', categories: [], complaintUrl: 'https://www.mazowieckie.com.pl/', complaintFormUrl: null, complaintChannels: null },
    { name: 'Inny Przewoźnik', coverage: 'NotCovered', categories: [], complaintUrl: null, complaintFormUrl: null, complaintChannels: null },
  ],
  categories: [],
  minimumPayout: 16,
  utk: {
    name: 'Prezes Urzędu Transportu Kolejowego',
    complaintUrl: 'https://utk.gov.pl/pl/urzad/kontakt',
    complaintChannels: 'E-mailem na adres pasazer@utk.gov.pl.',
  },
}

beforeEach(() => {
  vi.spyOn(api, 'getReference').mockResolvedValue(reference)
})

const expectExternal = (link: HTMLElement, href: string) => {
  expect(link.getAttribute('href')).toBe(href)
  expect(link.getAttribute('target')).toBe('_blank')
  expect(link.getAttribute('rel')).toBe('noopener noreferrer')
  expect(link.querySelector('svg')).toBeTruthy()
}

describe('WhereToSend', () => {
  it('lists the verified channels and links of the carrier, plus the generic advice', async () => {
    render(<WhereToSend carrier="PKP Intercity" recipient="carrier" />)

    expect(screen.getByRole('heading', { name: 'Gdzie wysłać pismo' })).toBeTruthy()
    expect(await screen.findByText('Formularz online albo pocztą.')).toBeTruthy()
    expectExternal(
      screen.getByRole('link', { name: /Strona przewoźnika: intercity\.pl/ }),
      'https://www.intercity.pl/pl/site/dla-pasazera/informacje/reklamacje-i-skargi/info.html',
    )
    expectExternal(
      screen.getByRole('link', { name: /Formularz reklamacyjny \(PDF\)/ }),
      'https://www.intercity.pl/pl/dokumenty/formularze/Formularz.pdf',
    )
    expect(screen.getByText(GENERIC_SEND_ADVICE)).toBeTruthy()
  })

  it('matches the carrier name regardless of case and shows a home page alone', async () => {
    render(<WhereToSend carrier="koleje mazowieckie" recipient="carrier" />)

    expectExternal(await screen.findByRole('link', { name: /mazowieckie\.com\.pl/ }), 'https://www.mazowieckie.com.pl/')
    expect(screen.queryByRole('link', { name: /PDF/ })).toBeNull()
  })

  it('shows only the generic advice for a carrier without verified links or a typed-in one', async () => {
    const { unmount } = render(<WhereToSend carrier="Inny Przewoźnik" recipient="carrier" />)
    await screen.findByText(GENERIC_SEND_ADVICE)
    await vi.waitFor(() => expect(api.getReference).toHaveBeenCalled())
    expect(screen.queryByRole('link')).toBeNull()
    unmount()

    render(<WhereToSend carrier="Leo Express" recipient="carrier" />)
    expect(await screen.findByText(GENERIC_SEND_ADVICE)).toBeTruthy()
    expect(screen.queryByRole('link')).toBeNull()
  })

  it('keeps the generic advice when the reference data cannot be loaded', async () => {
    vi.mocked(api.getReference).mockRejectedValue(new ApiError(0, 'Brak połączenia.'))
    render(<WhereToSend carrier="PKP Intercity" recipient="carrier" />)

    expect(await screen.findByText(GENERIC_SEND_ADVICE)).toBeTruthy()
    expect(screen.queryByRole('link')).toBeNull()
  })

  it('sends a complaint to the regulator to UTK, not to the carrier', async () => {
    render(<WhereToSend carrier="PKP Intercity" recipient="utk" />)

    expect(screen.getByRole('heading', { name: 'Gdzie wysłać skargę' })).toBeTruthy()
    expect(await screen.findByText('E-mailem na adres pasazer@utk.gov.pl.')).toBeTruthy()
    expectExternal(screen.getByRole('link', { name: /utk\.gov\.pl/ }), 'https://utk.gov.pl/pl/urzad/kontakt')
    expect(screen.queryByText('Formularz online albo pocztą.')).toBeNull()
    expect(screen.queryByText(GENERIC_SEND_ADVICE)).toBeNull()
  })
})
