# Architektura i szczegóły techniczne

Powrót do [README](../README.md). Nazwy `RefundPilot.*` w kodzie to robocza nazwa techniczna z początku prac; dla użytkownika produkt nazywa się Zwrotnica.

## Architektura i stack
Osobne projekty: `backend/` (ASP.NET Core, .NET 10) i `frontend/` (React + Vite + TypeScript).

**Backend – warstwy (Clean Architecture w wersji minimum):**

| Projekt | Odpowiedzialność |
|---|---|
| `RefundPilot.Domain` | Reguły bez zależności: `EligibilityService` (kto jest objęty systemem), `CompensationCalculator` (25/50/100%), `CarrierCatalog` (słownik przewoźników i kategorii, jedno źródło prawdy), `Iban` (mod-97), encje i enumy. |
| `RefundPilot.Application` | Serwisy przypadków użycia (`AssessmentService`, `CaseService`, `ExtractionService`, `ComplaintService`, `ProfileService`), DTO, walidacja wejścia (dozwolone znaki per pole, IBAN, data podróży), interfejsy (`IAiClient`, `ICaseRepository`, `IUnitOfWork`…). |
| `RefundPilot.Infrastructure` | EF Core + SQLite, repozytorium, transakcje (`EfUnitOfWork`), seed, `GeminiClient` / `NullAiClient`, szyfrowanie IBAN (`IbanCipher`), PDF (QuestPDF). |
| `RefundPilot.Api` | Chude kontrolery (przyjmują żądanie, wołają serwis, zwracają kod HTTP), konfiguracja: Identity, CORS, rate limiting, ProblemDetails, Swagger. |

- **Dane:** EF Core + SQLite (plik lokalny). Schemat powstaje przy starcie (`EnsureCreated`), a seed tworzy konto demo z 5 fikcyjnymi sprawami. Zapisy obejmujące kilka tabel idą w transakcji, a listy są pobierane bez zapytań w pętlach.
- **Uwierzytelnianie:** ASP.NET Core Identity (`MapIdentityApi`) z ciasteczkiem `HttpOnly`, `SameSite=Lax`. API zwraca 401/403 zamiast przekierowań. Każda sprawa należy do konta: zapytania filtrują po użytkowniku, a cudza sprawa daje 404.
- **Szyfrowanie IBAN:** ASP.NET Core Data Protection (osobny „purpose” dla rachunku).
- **Błędy:** spójny format ProblemDetails (`application/problem+json`) z polskimi komunikatami, 201 + `Location` przy tworzeniu, 204 przy usuwaniu, 409 dla stanów, w których akcja nie ma sensu (np. pismo dla sprawy bez ceny biletu albo odczyt biletu bez klucza AI).
- **Rate limiting:** odczyt biletu przez AI to stałe okno, domyślnie 5 żądań na minutę na adres IP.
- **Swagger** pod `/swagger` (w trybie Development).
- **Frontend:** React 19, Vite, TypeScript, `react-router`, ikony `lucide-react`, fonty IBM Plex Sans/Mono hostowane lokalnie (`@fontsource`, bez Google Fonts). Style to zwykły CSS na tokenach (`styles/tokens.css`), bez frameworka UI. Jeden moduł klienta HTTP (`src/api/client.ts`) z typami kontraktu (`src/api/types.ts`). Testy: Vitest + Testing Library.
- **CORS** jest jawnie skonfigurowany dla `http://localhost:5173`. W trybie dev frontend łączy się z API przez proxy Vite (`/api` → `http://localhost:5080`), więc ciasteczko logowania działa w obrębie jednego originu.

```
 Przeglądarka (React, :5173)
        │  /api/*  (proxy Vite, to samo origin → ciasteczko Identity)
        ▼
 ASP.NET Core API (:5080)
   Kontroler ──► Serwis (Application) ──► Reguły (Domain)        ← werdykt i kwoty: deterministycznie
                    │        │
                    │        └──► IAiClient ──► Gemini API        ← tylko: odczyt biletu, treść pisma
                    │                (NullAiClient bez klucza)       wynik walidowany schematem i regułami
                    └──► Repozytorium / UnitOfWork ──► SQLite
                              (IBAN szyfrowany przez Data Protection)
```


## Ciekawsze rozwiązania
### „AI czyta, kod liczy”

To główna zasada projektu i mój „creative twist”: AI robi tylko to, czego regułami nie da się zrobić sensownie, a wszystko, co ma skutki prawne lub finansowe, jest deterministyczne i przetestowane.

- **LLM tylko odczytuje dane z biletu i pisze treść pisma.** O tym, czy odszkodowanie się należy, ile wynosi i czy jest ryzyko odmowy, decydują `EligibilityService`, `CompensationCalculator` i `AssessmentService`. Serwer zawsze liczy to od nowa, także przy zapisie sprawy.
- **Wyjście AI jest walidowane.** Odczyt biletu wymusza JSON Schema (`responseJsonSchema`), a serwer i tak sprawdza typy, długości i znaki. Przewoźnika i kategorię mapuje na słownik, więc AI nie wprowadzi nowego przewoźnika, który niechcący „kwalifikuje się”.
- **Pismo od AI przechodzi kontrolę, zanim trafi do użytkownika.** Musi zawierać dokładnie wyliczoną kwotę, odwołanie do 2021/782 i właściwy artykuł (art. 19 przy opóźnieniu, art. 18 przy odwołaniu). Odrzucane jest pismo, które zawiera inne sumy niż kwota i cena, ciąg podobny do numeru rachunku (IBAN albo 26 cyfr, także ze spacjami), wyłączające sformułowania o formie wypłaty, nieznane lub źle zbudowane tokeny albo tekst bez polskich znaków (np. temat „z tytulu”: typowe słowa bez ogonków jak „prosze”, „pociagu” albo dłuższa treść bez żadnej litery z ogonkiem poza danymi sprawy). W każdym takim przypadku, a także przy awarii AI, pismo powstaje z szablonu.
- **Zdanie o formie wypłaty wstawia serwer** w miejsce tokenu, więc zawsze brzmi dokładnie tak, jak ustalono dla wybranej opcji.
- **Dane osobowe z profilu nie trafiają do AI.** Model dostaje tylko fakty sprawy i pisze tokeny `{{FULL_NAME}}`, `{{ADDRESS}}` itd., które serwer zastępuje oczyszczonymi danymi z profilu. Brakujące dane stają się lukami `[ ]` i trafiają na listę do uzupełnienia. Wyjątek: zdjęcie biletu wysłane do odczytu idzie do Gemini w całości, dlatego przy wgrywaniu UI podpowiada, że dane osobowe na zdjęciu można zasłonić (albo wpisać dane ręcznie).

### Pozostałe

- **Uczciwe werdykty zamiast optymistycznych.** Regionalny pociąg dostaje „Nie należy się” z wyjaśnieniem, sama kategoria InterRegio bez Polregio i bilet okresowy „Do sprawdzenia”, brak ceny oznacza brak zgadywanej kwoty. Dla przewoźnika spoza systemu albo niejasnej sprawy aplikacja przygotowuje reklamację według regulaminu przewoźnika, bez żądania kwoty (osobny prompt AI i osobna walidacja: tylko cena biletu jako kwota, bez art. 18/19 i bez powoływania rozporządzenia 2021/782, z fallbackiem do szablonu). Tak samo dla objętego pociągu, przy którym przepisy UE nic nie dają (opóźnienie poniżej 60 minut).
- **Dozwolone znaki per pole, lustrzane w FE i BE.** Każde pole (imię, adres, stacja, numer biletu, IBAN, notatka…) ma własną listę dozwolonych znaków, bez emoji i symboli. Frontend odrzuca niedozwolone znaki przy wpisywaniu i wklejaniu, z obsługą IME i klawiszy martwych. API odrzuca je z kodem 400. „Inteligentna” interpunkcja z klawiatur telefonów (cudzysłowy drukarskie, półpauzy, twarda spacja) jest zamieniana na zwykłą po obu stronach, tak samo (`TextRules.cs` ↔ `textRules.ts`).
- **IBAN:** walidacja sumy kontrolnej mod-97 (FE i BE) i szyfrowanie w bazie przez Data Protection. Numer widzi tylko właściciel po zalogowaniu.
- **Jedna sprawa na jedno przejście kreatora.** Ponowne „Zapisz” lub błąd w połowie nie tworzą duplikatów. Stan kreatora jest powiązany z kontem, więc po wylogowaniu nic nie przecieka do następnej osoby.
- **Odliczanie terminu w strefie Europe/Warsaw.** Daty są liczone tak, jak czyta je pasażer w Polsce, niezależnie od strefy urządzenia i serwera (także „dzisiaj” przy walidacji daty podróży).
- **Przypomnienie e-mail po terminie przewoźnika.** Usługa w tle (`ReminderBackgroundService`) przy starcie i co godzinę woła `ReminderService`. Jedno zapytanie zwraca sprawy „Wysłana”, których termin skończył się przed dzisiejszym dniem w Europe/Warsaw (przewoźnik ma cały ostatni dzień, art. 112 KC), bez przypomnienia i z włączoną zgodą właściciela, razem z jego adresem e-mail. Potem **claim-then-send**: jeden warunkowy `UPDATE ... SET ReminderSentAt = teraz WHERE ReminderSentAt IS NULL` rezerwuje partię, a wysyłane są tylko sprawy zarezerwowane przez ten przebieg. Dwie instancje nie wyślą więc tego samego e-maila (najwyżej raz); kosztem jest to, że awaria procesu między rezerwacją a wysyłką gubi to jedno przypomnienie. `ReminderSentAt` oznacza „obsłużone”: wysłane albo trwale niedostarczalne (zarezerwowana domena testowa, błędny adres, odmowa skrzynki 550/551/553). Błąd przejściowy zwalnia rezerwację na następny przebieg, a niedostępny serwer SMTP (połączenie, TLS, logowanie, 15 s timeout) przerywa przebieg. Logi zawierają tylko id sprawy i typ błędu, nigdy adres ani treść.
- **Tryb bez AI.** Bez klucza rejestrowany jest `NullAiClient`: wgrywanie biletu zwraca 409, UI przełącza na ścieżkę ręczną, a pismo powstaje z szablonu. Limit (429) i błąd AI (502) kończą się tak samo, czytelnym komunikatem i trybem ręcznym. Aplikacja nigdy się nie wysypuje.


## Bezpieczeństwo

- Ciasteczko Identity: `HttpOnly`, `SameSite=Lax`, w produkcji `Secure` i HSTS. API zwraca 401/403 zamiast przekierowań.
- Każda sprawa należy do konta, cudza sprawa daje 404.
- Numer rachunku szyfrowany Data Protection (osobny purpose), widoczny tylko dla właściciela.
- Dane osobowe z profilu nie trafiają do AI. Wyjątek: zdjęcie biletu wysłane do odczytu.
- Limit odczytów AI na IP; za proxy IP pochodzi z `X-Forwarded-For` (patrz `ForwardedHeaders:Enabled`).
- Walidacja wejścia po obu stronach (dozwolone znaki, IBAN mod-97); typ pliku rozpoznawany po `Content-Type`.

## Konfiguracja

| Klucz | Domyślnie | Znaczenie |
|---|---|---|
| `GEMINI_API_KEY` / `Gemini:ApiKey` | brak | Klucz Gemini. Bez niego aplikacja działa w trybie ręcznym. |
| `Gemini:Model` | `gemini-3.5-flash-lite` | Model Gemini. |
| `RateLimiting:ExtractionsPerMinute` | `5` | Limit odczytów biletu na adres IP na minutę. |
| `Seed:DemoEmail`, `Seed:DemoPassword` | w `appsettings.Development.json` | Konto demo. Bez nich seed się nie wykonuje. |
| `Seed:Enabled` | `true` (produkcyjnie `false`) | Czy w ogóle tworzyć konto demo. |
| `DataProtection:KeysPath` | domyślny katalog użytkownika (produkcyjnie `/home/data/keys`) | Gdzie trzymać klucze szyfrujące IBAN. |
| `ConnectionStrings:Default` | `DataSource=refundpilot.db` (produkcyjnie `/home/data/zwrotnica.db`) | Plik bazy SQLite. |
| `PublicBaseUrl` | brak (w Development `http://localhost:5173`) | Adres frontendu do linku w e-mailu z przypomnieniem. Poza Development musi być pełnym adresem http(s), inaczej przypomnienia są wyłączone (błąd w logu). |
| `Email:Smtp:Host`, `Port`, `User`, `Password`, `EnableSsl` | brak, `587`, brak, brak, `true` | Serwer SMTP (STARTTLS). Bez hosta e-maile są zapisywane jako `.eml` w `Email:OutboxPath`. |
| `Email:From`, `Email:FromName` | `no-reply@zwrotnica.test`, `Zwrotnica` | Nadawca przypomnień. |
| `Email:OutboxPath` | `data/outbox` (produkcyjnie `/home/data/outbox`) | Folder na pliki `.eml`, gdy SMTP nie jest ustawiony. |
| `Reminders:Enabled` | w Development włączone; poza nim tylko gdy ustawiono `Email:Smtp:Host` | Jawne `true`/`false` ma pierwszeństwo (np. `true` bez SMTP zapisuje `.eml` w outboxie). |
| `Reminders:IntervalMinutes` | `60` | Wysyłka przypomnień przy starcie i co podany czas. |
| `ForwardedHeaders:Enabled` | `false` (produkcyjnie `true`) | Czytanie IP klienta i schematu z `X-Forwarded-*` od proxy z adresu prywatnego (jeden przeskok). |

Wartości produkcyjne są w `appsettings.Production.json`. Zmienne środowiskowe używają `__` zamiast `:` (np. `Seed__DemoEmail`). Katalog `/home/data` istnieje tylko na Azure App Service: uruchamiając wersję produkcyjną gdzie indziej, ustaw `ConnectionStrings__Default` (np. `DataSource=/var/lib/zwrotnica/zwrotnica.db`) i `DataProtection__KeysPath` na zapisywalne ścieżki.


## API REST


| Metoda | Ścieżka | Autoryzacja | Do czego |
|---|---|---|---|
| GET | `/api/health` | – | Sprawdzenie, czy aplikacja działa: `{ "status": "ok" }` |
| GET | `/api/reference` | – | Słownik przewoźników i kategorii oraz minimalna wypłata (`minimumPayout`), cache 1 h |
| POST | `/api/extractions` | – (limit 5/min/IP) | Odczyt biletu przez AI z 1–4 plików (multipart `files`). 409 bez klucza AI, 502 przy błędzie AI, 429 po przekroczeniu limitu |
| POST | `/api/assessments` | – | Werdykt, kwota, procent, ryzyko odmowy i werdykt końcowy (`outcome`) dla podanych danych (bez zapisu) |
| POST | `/api/auth/register` | – | Rejestracja (Identity API) |
| POST | `/api/auth/login?useCookies=true` | – | Logowanie, ustawia ciasteczko (Identity API) |
| GET | `/api/auth/manage/info` | ciasteczko | Dane zalogowanego użytkownika (Identity API) |
| POST | `/api/auth/logout` | ciasteczko | Wylogowanie (204) |
| GET | `/api/profile` | ciasteczko | Profil: imię i nazwisko, adres, rachunek, forma wypłaty |
| PUT | `/api/profile` | ciasteczko | Aktualizacja profilu |
| GET | `/api/cases` | ciasteczko | Lista spraw użytkownika |
| GET | `/api/cases/{id}` | ciasteczko | Szczegóły sprawy z werdyktem końcowym (`outcome`), 404 dla cudzej |
| POST | `/api/cases` | ciasteczko | Utworzenie sprawy. Werdykt i kwotę liczy serwer (201 + `Location`) |
| PUT | `/api/cases/{id}` | ciasteczko | Edycja sprawy (przeliczenie werdyktu) |
| PATCH | `/api/cases/{id}/status` | ciasteczko | Zmiana statusu. „Wysłana” ustawia termin odpowiedzi (+1 miesiąc) |
| DELETE | `/api/cases/{id}` | ciasteczko | Usunięcie sprawy (204) |
| GET | `/api/stats` | ciasteczko | Kwoty odzyskane i oczekujące, liczba spraw |
| POST | `/api/cases/{id}/complaint` | ciasteczko | Wygenerowanie pisma: `{ payout, oneTimeIban?, saveIbanToProfile }` → temat, treść, lista braków. Gdy nic nie należy się według przepisów UE (także opóźnienie poniżej 60 minut) albo sprawa jest niejasna: reklamacja wg regulaminu przewoźnika (bez kwoty). 409 tylko dla sprawy bez ceny biletu |
| POST | `/api/cases/{id}/complaint/pdf` | ciasteczko | PDF z podanego tematu i treści (bez wywołania AI, bez zapisu) |

Pozostałe endpointy `MapIdentityApi` (np. `refresh`, `forgotPassword`, `manage/2fa`) są wystawione przez framework, ale frontend ich nie używa.


## Struktura repozytorium

.
├── backend/
│   ├── RefundPilot.sln
│   ├── src/
│   │   ├── RefundPilot.Domain/          # reguły: uprawnienia, kwoty, słownik przewoźników, IBAN
│   │   ├── RefundPilot.Application/     # serwisy, DTO, walidacja, interfejsy
│   │   ├── RefundPilot.Infrastructure/  # EF Core + SQLite, Gemini, PDF, szyfrowanie, seed
│   │   └── RefundPilot.Api/             # kontrolery, Program.cs, konfiguracja
│   └── tests/RefundPilot.Tests/         # xUnit: Domain, Application, Api
├── frontend/
│   └── src/
│       ├── api/        # klient HTTP + typy kontraktu
│       ├── wizard/     # kreator: bilet, opóźnienie, wynik, pismo
│       ├── cases/      # lista i szczegóły spraw, odliczanie terminu
│       ├── auth/ profile/ home/ components/ hooks/ lib/ styles/ theme/ routes/
├── docs/
│   ├── demo.gif, screenshots/
│   ├── research.md, interview-decisions.md, design-direction.md, product-notes.md
│   └── superpowers/    # specyfikacja i plan implementacji
├── tools/demo/         # skrypty Playwright do nagrania dema
├── instructions.md     # instrukcje dla agenta AI
├── skills-lock.json    # umiejętności agenta używane w projekcie
└── .env.example
```

## Testy

- **Backend** (xUnit, `backend/tests/RefundPilot.Tests`): reguły domeny, serwisy, walidacja, kontrolery przez `WebApplicationFactory`. Bez realnych wywołań AI.
- **Frontend** (Vitest + Testing Library): kreator, formularze, reguły znaków, lista spraw.
- CI: `.github/workflows/ci.yml` uruchamia testy backendu i frontendu.
