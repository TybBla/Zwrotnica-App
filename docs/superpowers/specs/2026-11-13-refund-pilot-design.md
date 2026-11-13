# Refund Pilot – dokument projektowy (v2, po researchu i wywiadzie)

Data: 2026-11-13 · Status: **do akceptacji** · Termin oddania: 2026-11-15, 23:59
Źródła: `docs/research.md` (prawo, Gemini), `docs/interview-decisions.md` (decyzje użytkownika).

## 1. Po co (intencja)

Pasażer pociągu dalekobieżnego z dużym opóźnieniem zwykle nie wie, że przysługuje mu odszkodowanie (rozp. UE 2021/782: opóźnienie w stacji docelowej 60–119 min = 25 %, ≥120 min = 50 % ceny zapłaconej za bilet), a jeśli wie, zniechęca go pisanie reklamacji.

**Refund Pilot** w 3 prostych krokach zamienia "mam bilet i spóźniłem się 95 minut" w uczciwy werdykt, kwotę i gotowe pismo. AI czyta bilet i pisze pismo; kod rozstrzyga o prawie i liczy kwoty.

**Główny cel produktowy:** każdy element funkcjonalny i komfort użycia na najwyższym poziomie; kreator czytelny od pierwszego spojrzenia.

## 2. Cele, nie-cele, antycele

**Cele**
- Kreator 3 kroków: **Bilet → Opóźnienie → Wynik** (+ odbiór pisma). Na starcie wybór: *wgraj bilet* (AI uzupełnia, użytkownik weryfikuje i dopełnia braki) albo *wpisz ręcznie*.
- Uczciwy werdykt: **Należy się / Nie należy się / Do sprawdzenia**, z krótkim uzasadnieniem (np. pociąg regionalny = poza systemem).
- Pismo reklamacyjne: temat e-maila + treść do wklejenia **lub** pobierany PDF.
- Lista spraw (CRUD), statusy Nowa → Wysłana → Wypłacona/Odrzucona, licznik odzyskanych kwot, **odliczanie terminu odpowiedzi przewoźnika** (1 miesiąc od wysłania) i po terminie podpowiedź eskalacji do UTK.
- Opcjonalny profil użytkownika (imię, adres, konto, ostatnio wybrana forma wypłaty) tylko w przeglądarce. Profil jest przypisany do przeglądarki/urządzenia (brak kont i synchronizacji między urządzeniami); opcjonalny eksport/import profilu do pliku JSON.
- **Forma wypłaty do wyboru przez użytkownika: gotówka lub bon.** Ostatni wybór jest zapamiętywany w profilu i zaznaczany domyślnie.
- **Mobile-first:** pełna wygoda na telefonie (aparat jako źródło zdjęcia biletu, duże cele dotykowe, przyklejony przycisk akcji).
- Dwa motywy: jasny fintech i ciemny szklany, z przełącznikiem.
- Działa bez klucza AI (ścieżka ręczna, pismo z szablonu).

**Nie-cele (MVP):** autobusy, komunikacja miejska, loty; logowanie i konta; wysyłka reklamacji przez serwer; przechowywanie zdjęć; upload dowodu opóźnienia (użytkownik wpisuje liczbę minut); mailto.

**Antycele:** AI liczy kwoty lub zgaduje brakujące dane; obiecywanie pieniędzy, które się nie należą; reviewer nie uruchomi projektu w kilka minut; UI wygląda jak szablon z doklejonym chatbotem; dane osobowe wysyłane do AI.

## 3. Ograniczenia

- SQLite (plik lokalny) – brak zewnętrznych baz; darmowe AI, klucz opcjonalny (`GEMINI_API_KEY`).
- Zasady z `instructions.md` (chude kontrolery, serwisy, DTO, brak zapytań w pętlach, transakcje).
- Zdjęcia biletu nie są zapisywane. Profil użytkownika nie jest zapisywany na serwerze ani wysyłany do AI.
- Demo używa darmowego Gemini (treści z darmowego poziomu mogą służyć do ulepszania produktów Google). W README zaznaczone, że produkcyjnie użyty byłby model komercyjny z właściwą konfiguracją prywatności; w UI tylko krótka wzmianka.
- Dane w seedach i demo wyłącznie fikcyjne.

## 4. Podejścia

| | Opis | Wady |
|---|---|---|
| A. OCR + regexy | Tesseract + wzorce | Kruche, brak "wow" |
| B. LLM robi wszystko | Model czyta, liczy, rozstrzyga | Błędy w arytmetyce i prawie, nie do obrony |
| **C. LLM czyta bilet i pisze pismo; kod rozstrzyga i liczy (wybrane)** | JSON ze schematem, walidacja, deterministyczny kalkulator i reguły uprawnień | Wymaga schematu i walidacji |

## 5. Reguły domenowe (kod, nie AI)

- **EligibilityService** → `Eligible | NotCovered | Unknown` wg przewoźnika i kategorii pociągu. Pokryte: EIP, EIC, IC, TLK, IR (PKP Intercity) i połączenia międzynarodowe. Wyłączone w Polsce: pociągi miejskie/podmiejskie/regionalne (Polregio, Koleje Mazowieckie, Śląskie, Dolnośląskie, Małopolskie, Wielkopolskie, SKM, WKD, ŁKA, Arriva RP) – werdykt "Nie należy się" z wyjaśnieniem. Nierozpoznany przewoźnik → "Do sprawdzenia".
- **CompensationCalculator:** 60–119 min = 25 %, ≥120 min = 50 %, odwołanie = zwrot 100 %; cena = faktycznie zapłacona; minimalna wypłata domyślnie **16 zł** (PKP Intercity; limit UE 4 EUR), poniżej – flaga "poniżej progu".
- **Przyczyna opóźnienia** (opcjonalna): nieznana / pogoda / osoby na torach / strajk personelu kolejowego / awaria techniczna / inna. Przyczyna **nie zmienia kwoty** (procent zależy tylko od długości opóźnienia), ale wpływa na ryzyko odmowy: pogoda i osoby na torach (siła wyższa) → flaga `RefusalRisk` i ostrzeżenie "przewoźnik może odmówić"; strajk personelu kolejowego, awaria techniczna, nieznana → bez flagi. Werdykt pozostaje "Należy się", przyczyna **nigdy nie blokuje** zgłoszenia.
- **Bilet okresowy** (checkbox przy ręcznym wpisie): kwota liczona inaczej niż dla biletu jednorazowego, więc werdykt "Do sprawdzenia", bez wyliczonej kwoty.
- Poza MVP (do wspomnienia w README): prawo do pomocy (posiłki, nocleg) niezależne od odszkodowania, zwrot ceny biletu, przekierowanie.
- **Termin odpowiedzi:** `ResponseDueDate = FiledAt + 1 miesiąc` ustawiany przy zmianie statusu na Wysłana.
- Pismo żąda wypłaty w formie wybranej przez użytkownika (gotówka przelewem lub bon); przy gotówce wprost zaznacza, że nie zgadza się na bon. Powołuje art. 19 rozp. 2021/782, zawiera pola z unijnego wspólnego formularza (dane przejazdu, numer biletu, czy już składano wniosek).
- Termin składania wniosku jest w źródłach niejednoznaczny: UI mówi ostrożnie "złóż jak najszybciej" i kieruje do przewoźnika.

## 6. Decyzje techniczne

**Stack (moje decyzje):** ASP.NET Core Web API (.NET 10 – zainstalowane), EF Core + SQLite, xUnit; React + Vite + TypeScript, zwykły CSS (tokeny); PDF po stronie serwera przez QuestPDF (weryfikacja licencji Community i polskich znaków przy budowie).

**AI:** Gemini `generateContent` (REST przez `HttpClient`) z `inlineData` i `responseMimeType=application/json` + `responseJsonSchema`; model z konfiguracji, domyślnie `gemini-3.5-flash-lite` (weryfikacja przy budowie). Za interfejsem `IAiClient`. Nieczytelne/niepewne pola → `null` → kreator prosi o uzupełnienie. 429/5xx/zły JSON → komunikat po polsku i przejście na wpisywanie ręczne. Bez klucza: ścieżka ręczna, pismo z szablonu, upload biletu wyłączony z komunikatem.

**Pismo:** AI dostaje wyłącznie fakty sprawy (bez danych osobowych) i zwraca temat + treść z tokenami `{{FULL_NAME}}`, `{{ADDRESS}}`, `{{IBAN}}`; serwer podstawia je z profilu przekazanego w żądaniu (nie zapisuje), a gdy brak profilu zostawia czytelne `[imię i nazwisko]`.

**Model danych (`Cases`):** Id, Carrier, TrainCategory, FromStation, ToStation, TicketNumber, TravelDate, TicketPrice, IncidentType, DelayMinutes, DelayCause?, IsSeasonTicket, Verdict, RefusalRisk, CompensationAmount, PayoutPreference, Status, FiledAt?, ResponseDueDate?, Notes, CreatedAt.

**API (`/api`):**
- `POST /extractions` (multipart: 1–4 zdjęcia/PDF-obraz biletu, ≤5 MB) → `ExtractionDto`
- `POST /assessments` (dane przejazdu + minuty) → werdykt, kwota, uzasadnienie (bez zapisu)
- `GET/POST /cases`, `GET/PUT/DELETE /cases/{id}`, `PATCH /cases/{id}/status`
- `POST /cases/{id}/complaint` (body: opcjonalny profil) → `{subject, body}`; `POST /cases/{id}/complaint/pdf` → plik PDF
- `GET /stats` → odzyskane, oczekujące, liczba spraw
- Swagger, ProblemDetails, CORS dla `http://localhost:5173`.

**Frontend:**
- Kreator: pasek postępu, duża strefa upuszczania biletu, formularz z walidacją i podświetleniem brakujących pól, wynik z animowaną kwotą i werdyktem, wybór formatu pisma (tekst / PDF).
- Pole przyczyny z ikonką ⓘ: opis po najechaniu, fokusie klawiatury i tapnięciu (dostępny, nie stale widoczny).
- Profil użytkownika w localStorage (mały panel), przełącznik motywu, lista spraw z odliczaniem terminu, puste/ładujące/błędne stany, responsywność.
- Skille w `.agents/skills` (apple-design, emil-design-eng, animate, choreograph, break-ui) prowadzą dopracowanie.

**Struktura repo:** `backend/` (Domain, Application, Infrastructure, Api, Tests), `frontend/`, `docs/`, `README.md`, `instructions.md`, `.env.example`; `.agents/`, `skills-lock.json` poza repo.

**Dane startowe (fikcyjne):** 4–5 spraw: IC 95 min, EIC 140 min, odwołanie, wypłacona, jedna regionalna z werdyktem "nie należy się".

**Testy:** xUnit: kalkulator, uprawnienia (carrier/kategoria), walidator ekstrakcji, fallback regułowy, generator pisma i podstawianie profilu, API (CRUD, limity uploadu, 502 z AI).

## 7. Kolejność i cięcia (2 dni)

1. Dzień 1: backend (domena, uprawnienia, CRUD, ekstrakcja, pismo+PDF, testy) + szkielet kreatora.
2. Dzień 2: UI i motywy, lista z odliczaniem, polish, README, GIF, repo.
3. Bonus: darmowy deploy.
Cięcia w kolejności: deploy → PDF (zostaje tekst) → odliczanie terminu → motyw ciemny → upload biletu (zostaje ręczne).

## 8. Zostawione wykonawcy

Nazwy klas, dokładne teksty pism i komunikatów, paleta i typografia w ramach dwóch motywów, szczegóły animacji, lista przewoźników/kategorii w `EligibilityService`.
