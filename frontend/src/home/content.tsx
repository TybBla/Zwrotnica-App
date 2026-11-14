import type { ReactNode } from 'react'

/*
 * Home page copy. Facts follow docs/research.md (rozporządzenie (UE) 2021/782, art. 3a ustawy o transporcie
 * kolejowym, PKP Intercity). Where sources disagree (the filing deadline) the copy says so instead of guessing.
 */

export type RuleOutcome = 'due' | 'refund' | 'none'

export interface Rule {
  situation: string
  detail?: string
  outcome: string
  kind: RuleOutcome
}

export const rules: Rule[] = [
  { situation: 'Opóźnienie 60–119 min', detail: 'na stacji docelowej', outcome: '25% ceny biletu', kind: 'due' },
  { situation: 'Opóźnienie od 120 min', detail: 'na stacji docelowej', outcome: '50% ceny biletu', kind: 'due' },
  { situation: 'Odwołanie pociągu', outcome: 'Zwrot pełnej ceny biletu', kind: 'refund' },
  { situation: 'Opóźnienie poniżej 60 min', outcome: 'Brak odszkodowania', kind: 'none' },
  {
    situation: 'Pociąg regionalny, podmiejski lub miejski w Polsce',
    detail: 'np. Polregio (wszystkie jego pociągi, także InterRegio), Koleje Mazowieckie, Koleje Śląskie, SKM',
    outcome: 'Brak automatycznego odszkodowania wg przepisów UE. Zwrot udowodnionej szkody (np. taksówki, noclegu) – przygotujemy wniosek.',
    kind: 'none',
  },
]

export interface PrepItem {
  title: string
  text: string
  optional?: boolean
}

export const preparation: PrepItem[] = [
  {
    title: 'Bilet',
    text: 'Zdjęcie, skan albo e-bilet w PDF. Bez zdjęcia wystarczą przewoźnik, data przejazdu i cena, którą faktycznie zapłaciłeś.',
  },
  {
    title: 'Opóźnienie w minutach',
    text: 'Liczy się przyjazd na stację docelową: o ile minut później niż w rozkładzie. Zanotuj godzinę przyjazdu.',
  },
  {
    title: 'Przyczyna opóźnienia',
    text: 'Jeśli ją znasz, np. z komunikatu w pociągu. Od niej zależy, czy przewoźnik może odmówić wypłaty.',
    optional: true,
  },
  {
    title: 'Numer rachunku',
    text: 'Tylko jeśli chcesz przelew na inny rachunek niż ten, z którego płaciłeś za bilet.',
    optional: true,
  },
]

export interface FaqItem {
  question: string
  answer: ReactNode
}

export const faq: FaqItem[] = [
  {
    question: 'Komu przysługuje odszkodowanie?',
    answer: (
      <>
        <p>
          Unijne zasady 25% i 50% obejmują pociągi dalekobieżne, czyli w PKP Intercity pociągi EIP, EIC, IC i TLK, oraz
          pociągi międzynarodowe. Liczy się opóźnienie na stacji docelowej: od 60 minut należy się 25% ceny biletu, od 120
          minut 50%.
        </p>
        <p>Kwotę liczymy od ceny faktycznie zapłaconej, czyli po uwzględnieniu ulgi.</p>
      </>
    ),
  },
  {
    question: 'Czy dotyczy to pociągów regionalnych?',
    answer: (
      <p>
        Nie. Polska wyłączyła pociągi miejskie, podmiejskie i regionalne z unijnego systemu 25%/50% (art. 3a ustawy o
        transporcie kolejowym). Dotyczy to polskich przewoźników regionalnych, m.in. Polregio (także jego pociągów
        InterRegio), Kolei Mazowieckich, Kolei Śląskich, Kolei Dolnośląskich, SKM i WKD. Tacy przewoźnicy zwykle
        uwzględniają tylko roszczenia o zwrot udowodnionej szkody, czyli kosztów, które poniosłeś przez opóźnienie lub
        odwołanie pociągu – np. taksówki, noclegu albo biletu na inne połączenie. Jeśli je wpiszesz, Zwrotnica przygotuje
        wniosek o zwrot tych kosztów; dołącz do niego rachunki. Bez kosztów przygotujemy reklamację według regulaminu
        przewoźnika, bez żądania konkretnej kwoty. Jeśli znasz tylko kategorię InterRegio (IR), a przewoźnik jest inny
        niż Polregio albo go nie podasz, wynik to „Do sprawdzenia”.
      </p>
    ),
  },
  {
    question: 'Co, jeśli opóźnienie spowodowała pogoda albo strajk?',
    answer: (
      <>
        <p>
          Przewoźnik nie musi płacić, jeśli opóźnienie wynikło z siły wyższej, np. ekstremalnej pogody, osób na torach,
          kradzieży kabli lub sabotażu. Strajk personelu kolejowego nie jest siłą wyższą, więc odszkodowanie wtedy
          przysługuje.
        </p>
        <p>Odszkodowanie nie należy się także, jeśli o opóźnieniu wiedziałeś przed zakupem biletu.</p>
      </>
    ),
  },
  {
    question: 'W jakiej formie dostanę pieniądze?',
    answer: (
      <p>
        Domyślnie wybieramy zwrot na konto lub kartę, którymi zapłaciłeś za bilet. Możesz też wskazać przelew na inny
        rachunek albo bon na kolejne przejazdy. Przewoźnicy często proponują bon. Jeśli wolisz pieniądze, pismo wprost
        prosi o wypłatę.
      </p>
    ),
  },
  {
    question: 'Czy jest minimalna kwota wypłaty?',
    answer: (
      <p>
        Tak. Przewoźnik może nie wypłacać kwot poniżej ustalonego progu (do 4 euro). W PKP Intercity próg wynosi 16 zł.
        Jeśli kwota wyjdzie niższa, uprzedzimy Cię o tym w wyniku.
      </p>
    ),
  },
  {
    question: 'Ile mam czasu na złożenie reklamacji?',
    answer: (
      <>
        <p>
          Złóż ją jak najszybciej. Źródła podają różne terminy, dlatego nie zwlekaj dłużej niż 3 miesiące i sprawdź
          regulamin swojego przewoźnika. Przewoźnik powinien wypłacić odszkodowanie w ciągu miesiąca od złożenia wniosku.
        </p>
        <p>
          Jeśli odmówi albo nie odpowie, możesz zwrócić się do Prezesa Urzędu Transportu Kolejowego lub do Rzecznika Praw
          Pasażera Kolei.
        </p>
      </>
    ),
  },
  {
    question: 'Czy muszę zakładać konto?',
    answer: (
      <p>
        Nie, żeby sprawdzić, ile Ci się należy. Konto jest potrzebne dopiero do zapisania sprawy i przygotowania pisma.
        Wtedy to, co wpisałeś, nie przepada.
      </p>
    ),
  },
  {
    question: 'Co dzieje się z moimi danymi?',
    answer: (
      <p>
        Zdjęć i plików biletu nie zapisujemy. Służą tylko do odczytania danych. Numer rachunku jest szyfrowany w bazie,
        widzisz go tylko Ty i nigdy nie trafia do AI.
      </p>
    ),
  },
]

export interface Source {
  label: string
  href: string
}

export const sources: Source[] = [
  {
    label: 'Rozporządzenie (UE) 2021/782 w sprawie praw i obowiązków pasażerów w ruchu kolejowym (EUR-Lex)',
    // ELI, the EU's persistent identifier for the act (survives EUR-Lex URL changes; on 2026-11-14 EUR-Lex sent both
    // this and the old CELEX deep link to its home page, a site-side redirect).
    href: 'https://eur-lex.europa.eu/eli/reg/2021/782/oj',
  },
  {
    label: 'Prawa pasażerów w ruchu kolejowym (Your Europe)',
    href: 'https://europa.eu/youreurope/citizens/travel/passenger-rights/rail/index_pl.htm',
  },
  { label: 'Urząd Transportu Kolejowego', href: 'https://utk.gov.pl/' },
]
