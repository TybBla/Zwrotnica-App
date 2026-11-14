# Refund Pilot – kierunek projektowy (v2)

Status: obowiązujący od 2026-11-14. Zastępuje pierwszą wersję wyglądu (Geist + fiolet `#4b45e0`, poświata gradientowa w hero, wyśrodkowany hero + trzy karty).

## Dlaczego zmiana

Decyzja użytkownika: pierwsza wersja wyglądała jak "typowy AI slop" (gradienty, struktura strony). Ma wyglądać jak komercyjny produkt dużej firmy, a nie jak kolejna wygenerowana strona.

Research (źródła na dole) wskazuje powtarzalne "znaki AI", z których pierwsza wersja miała prawie wszystkie: fioletowo-indygo akcent, gradientowa poświata, jeden "nowoczesny" font do wszystkiego, wyśrodkowany hero z pigułką i dwoma przyciskami, trzy równe karty, numerowane kroki-karty, szkło jako ozdoba, przykładowy wynik jako dekoracja. Zakaz samego fioletu nie wystarcza – agent przeskakuje do następnego "modnego" wyglądu (krem + terakota, czerń + kwasowa zieleń). Potrzebny jest **pozytywny kierunek wyprowadzony z produktu**.

## Kierunek: "usługa publiczna klasy premium dla pasażera kolei"

Produkt to narzędzie do załatwienia sprawy (reklamacja), nie kampania marketingowa. Wzorce: polskie i europejskie serwisy kolejowe i finansowe, GOV.UK Design System (formularze), aplikacje bankowe. Spokój, czytelność, zaufanie, konkret.

### Kolor – wyprowadzony z polskiej kolei
- **Granat (ink navy)** – kolor marki, przyciski główne, nagłówki. Inspiracja: plakaty rozkładów jazdy i oznakowanie dworców.
- **Żółty sygnałowy** – jedyny akcent, używany oszczędnie: wyróżnienie kwoty, aktywny krok, fokus. Inspiracja: żółty plakat "Odjazdy" na polskich stacjach.
- **Neutralne szarości** lekko chłodne; tło jasnego motywu prawie białe (papier), ciemnego – głęboki granat (nie czerń).
- **Semantyka werdyktu** jak sygnalizacja kolejowa: zielony (Należy się), czerwony stonowany (Nie należy się), bursztynowy (Do sprawdzenia).
- Zakazy: gradienty (tła, tekst, poświaty), glow, fiolet/indygo, kolorowanie pojedynczego słowa w nagłówku.

### Typografia
- **IBM Plex Sans** (UI i treść) – inżynierski, korporacyjny charakter, pełne polskie znaki. **IBM Plex Mono** wyłącznie dla danych biletu i kwot w tabelach (numer biletu, IBAN) – nie jako ozdobne etykiety.
- Fonty **hostowane lokalnie** (pakiety `@fontsource`), bez Google Fonts – zgodnie z podejściem do prywatności.
- Skala typograficzna stała; nagłówki bez wersalików z rozstrzelonym trackingiem, bez "eyebrow" etykiet nad treścią.

### Układ
- **Strona główna = narzędzie.** Pierwszy ekran od razu zaczyna sprawę: krótki konkretny nagłówek ("Opóźniony pociąg? Sprawdź, ile Ci się należy") i panel startowy z dwoma ścieżkami (wgraj bilet / wpisz ręcznie). Na desktopie układ dwukolumnowy, wyrównany do lewej; nie wyśrodkowany hero.
- Pod spodem treść, która ma wartość, a nie dekorację: **tabela zasad** (60–119 min → 25 %, ≥120 min → 50 %, odwołanie → zwrot; pociągi regionalne bez odszkodowania wg przepisów UE), **co przygotować**, **FAQ** z prawdziwymi odpowiedziami, stopka z zastrzeżeniem prawnym i źródłami (rozp. 2021/782, UTK).
- Numeracja tylko dla prawdziwej sekwencji (kroki kreatora). Wiersze i tabele dla treści tabelarycznej zamiast siatki kart.
- Karty: promień 8–12 px, obramowanie 1 px, cień tylko dla elementów faktycznie uniesionych (menu, dialog). Bez zagnieżdżania kart w kartach.
- Szkło (blur) usunięte; nagłówek pełny kolor tła z dolną linią.

### Elementy z charakterem produktu (z umiarem)
- Wynik kreatora może przypominać **bilet/pokwitowanie** (kwota jako główna liczba, dane przejazdu w siatce, subtelna perforacja) – bo to dokument dotyczący biletu, nie ozdoba.
- Ikony z jednej spójnej biblioteki (`lucide-react`, stały stroke), tylko gdy niosą znaczenie.

### Ruch
- Animacja tylko w odpowiedzi na działanie użytkownika (przejście kroku, rozwinięcie). Brak animacji przy przewijaniu, brak liczników "count-up" dla ozdoby (kwota wyniku może się pojawić z krótkim przejściem). Zawsze `prefers-reduced-motion`.

### Tekst
- Przyciski mówią, co robią ("Wgraj bilet", "Zapisz sprawę i przygotuj pismo"), nie "Zacznij".
- Bez wymyślonych liczb i ocen ("tysiące zadowolonych"). Bez emoji. Konkret zamiast przymiotników.

## Niezmienne (wcześniejsze decyzje użytkownika)
- Jeden język projektowy, dwie palety (jasna i ciemna) – różnią się tylko kolorami.
- Mobile-first, cele dotykowe ≥ 44 px, przyklejony pasek akcji na telefonie.
- Ikonka ⓘ z opisem po najechaniu / fokusie / tapnięciu.

## Źródła
- Capital & Compute – How to Fix AI Slop in Web Design, Tell by Tell: https://capitalandcompute.net/blog/fix-ai-slop-design/
- Utsubo – The "Built with AI" Tell: 12 Signals That Drop Trust in 2026: https://www.utsubo.com/blog/built-with-ai-trust-signals-2026
- prg.sh – Why Your AI Keeps Building the Same Purple Gradient Website: https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website
- GOV.UK Design System – Question pages: https://design-system.service.gov.uk/patterns/question-pages/
- AirHelp – przepływ sprawdzania odszkodowania (zdjęcie dokumentu → wynik z kwotą i regułą → dane osobowe dopiero na końcu): https://www.airhelp.com/en/ai-info/
