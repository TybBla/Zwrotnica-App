# AI Agent Instructions – Xelerit Recruitment Task (Full-Stack MVP)

Zawsze stosuj się do poniższych zasad podczas generowania kodu i analizy problemów. Ten plik jest commitowany do repozytorium (wymóg zadania: pokazujemy, jak skonfigurowano narzędzia AI).

## 0. Kontekst zadania (twarde wymagania)

- **Cel:** mała, pełnostosowa aplikacja webowa (Full-Stack MVP) o tematyce **transportu**. Rekrutacja: Xelerit.
- **Termin:** wysyłka na recruitment@xeler.it do **15.11.2026, 23:59** (link do publicznego repo GitHub + imię i nazwisko). Zadanie ma być do zrobienia w krótkim czasie – **żadnego over-engineeringu**.
- **Back-end:** dowolny język/framework; REST API z podstawowym **CRUD** dla wybranej funkcjonalności.
- **Front-end:** dowolny; musi płynnie komunikować się z API. **CORS skonfigurowany poprawnie.**
- **Baza:** wyłącznie **In-Memory lub SQLite**. Zakaz zewnętrznych serwerów bazodanowych (projekt ma startować bez konfiguracji).
- **AI:** mile widziane, ale musi mieć **realny sens użytkowy** (nie "byle gdzieś wrzucone"). Wystarczy jedna dobrze zaprojektowana funkcja/prompt albo prosty agent.
- **Chmura:** opcjonalny bonus (np. deploy dema), nigdy kosztem uruchamialności lokalnej.
- **Creative Twist:** jedna drobna, błyskotliwa, unikalna funkcja wyróżniająca aplikację.
- **Oceniane:** architektura i organizacja (osobne foldery FE/BE, warstwy), jakość i czytelność kodu, projekt REST API, UX/UI, kreatywność, rozumienie własnego rozwiązania.

## 1. Deliverables (nic nie może zostać pominięte)

1. Publiczne repo na GitHubie, z osobnymi folderami `backend/` i `frontend/`.
2. `README.md` zawierający:
   - krótki opis aplikacji i funkcjonalności,
   - opis techniczny (stack, ciekawsze rozwiązania),
   - **instrukcję krok po kroku** uruchomienia lokalnego (API i front-end),
   - opis, jak wspierałem się narzędziami AI (konkretnie: co i czym generowane).
3. Krótki film demo (`.gif` lub `.mp4`) w repozytorium.
4. Pliki instrukcji agentów zacommitowane (`instructions.md` / `AGENTS.md` / `CLAUDE.md` itp.).
5. Mail z linkiem + imię i nazwisko.

## 2. Zasady nadrzędne projektu

- **Uruchomienie < 5 min, zero konfiguracji obowiązkowej.** Reviewer klonuje repo i odpala dwie komendy. Klucz API do AI jest **opcjonalny**: bez niego aplikacja działa w trybie fallback/mock (komunikat w UI), nigdy nie crashuje.
- **Żadnych sekretów w repo.** Klucze tylko przez zmienne środowiskowe; w repo `.env.example`; `.env` w `.gitignore`.
- **AI ma znaczenie funkcjonalne:** LLM robi to, czego nie da się sensownie zrobić regułami (ekstrakcja z tekstu, generowanie dokumentu, wyjaśnienie). **Liczby i decyzje deterministyczne liczy kod (serwis), nie LLM.** Wyjścia LLM walidowane (structured output / JSON schema) przed użyciem.
- **Scope MVP:** kilka dopracowanych funkcji > wiele niedokończonych. Brak auth/użytkowników, chyba że niezbędne.
- **Demo-ready:** seed z sensownymi danymi, estetyczny UI, stany loading/empty/error obsłużone.
- Wszystko, co jest w repo, muszę umieć wytłumaczyć – preferuj proste, czytelne rozwiązania nad "sprytne".

## 3. Rygor architektury backendu

- **Zero "Fat Controllers":** kontrolery API tylko przyjmują żądanie HTTP, odbierają parametry i zwracają odpowiedź HTTP.
- **Warstwa serwisów:** cała logika biznesowa, obliczenia i wywołania zewnętrzne (w tym LLM, API pogodowe itp.) w dedykowanych serwisach wstrzykiwanych do kontrolerów (DI). Klient LLM za interfejsem (łatwy mock w testach i fallback).
- **DTO:** dane wchodzące i wychodzące zawsze mapowane przez DTO. Nigdy nie zwracaj encji/modeli bazy na zewnątrz ani nie zapisuj requestu prosto do bazy.
- **REST:** rzeczowniki w liczbie mnogiej, poprawne kody HTTP (201 + `Location`, 204, 400/404/422), spójny format błędów (np. ProblemDetails), walidacja wejścia, wersjonowanie/prefiks `/api`. Swagger/OpenAPI włączony.
- **CORS:** jawnie skonfigurowany dla originu frontendu (dev).

## 4. Baza danych i ORM

- **Ochrona przed N+1:** zawsze eager loading (`Include()` / `with()` / odpowiednik).
- **Zakaz zapytań w pętlach:** nigdy `Save`/`Update`/`Select` wewnątrz `foreach`/`for`/`while`. Przetwarzaj w pamięci lub operacjami wsadowymi.
- **Transakcje:** obowiązkowo dla zapisów obejmujących więcej niż jedną tabelę.
- SQLite (plik lokalny, w `.gitignore`) lub In-Memory; schemat tworzony automatycznie przy starcie + seed.

## 5. Frontend

- Osobny projekt w `frontend/`, TypeScript, typy zgodne z kontraktem API (najlepiej generowane/współdzielone z OpenAPI).
- Warstwa API (jeden moduł z klientem HTTP) oddzielona od komponentów; komponenty małe i czytelne.
- UX: responsywność, czytelne stany ładowania/błędów/pustych list, dostępność (labelki, kontrast, focus).
- Bez zbędnych bibliotek; każda zależność musi mieć powód.

## 6. Jakość kodu

- Czytelne nazwy, małe funkcje, brak martwego kodu i zakomentowanych bloków.
- Kilka sensownych testów tam, gdzie jest logika (serwisy obliczeniowe, parsowanie/walidacja wyjścia LLM). Testy bez realnych wywołań LLM.
- Commity małe i opisowe (najlepiej Conventional Commits), historia git ma pokazywać proces.
- Komentarze tylko tam, gdzie wyjaśniają *dlaczego*.

## 7. Czego unikać

- Fat controllers, encje w odpowiedziach API, zapytania w pętlach, brak transakcji.
- Zewnętrznych baz danych, Dockera jako wymogu uruchomienia, obowiązkowych kluczy/kont.
- Logiki biznesowej w LLM, "chatbota dla chatbota", niewalidowanego JSON-a z modelu.
- Sekretów w repo, zbędnych zależności, rozbudowy ponad MVP.
- Halucynowania: jeśli brakuje krytycznych informacji – pytaj.

## 8. Styl odpowiedzi agenta ("Zero Yapping")

- Odpowiadaj maksymalnie zwięźle, najlepiej samym kodem. Język rozmowy: polski; kod, nazwy, commity: angielski.
- Omijaj grzecznościowe wstępy, podsumowania i tłumaczenie podstaw programowania.
- Podawaj tylko pliki/funkcje, które uległy zmianie (diff), bez zbędnego boilerplate.
- Jeśli brakuje krytycznych informacji do poprawnego napisania kodu – zatrzymaj się i zapytaj zamiast zgadywać.
