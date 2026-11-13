# Research – Refund Pilot (2026-11-13)

Źródła: UTK/UniQA (przegląd zasad), europa.eu "Prawa pasażerów w ruchu kolejowym", strony PKP Intercity (formularz 06/2024, skargi i reklamacje), dokumentacja Gemini API (aistudio.google.com/docs). Wszystko poza literą rozporządzenia to źródła wtórne – **przed publikacją zweryfikować na oficjalnych stronach**.

## 1. Prawo (wpływa na logikę aplikacji)

| Ustalenie | Skutek dla projektu |
|---|---|
| Rozp. (UE) 2021/782 (od 7.06.2023): opóźnienie na stacji docelowej 60–119 min = 25 %, ≥120 min = 50 % ceny biletu (faktycznie zapłaconej, po zniżkach) | Potwierdza `CompensationCalculator`; cena = zapłacona, nie katalogowa |
| Minimalna kwota wypłaty ustalana przez przewoźnika, max 4 EUR; **PKP Intercity: 16 zł** | `MinimumPayout` konfigurowalny per przewoźnik; domyślnie 16 zł (nie 17) |
| **Polska wyłącza z systemu 25/50 pociągi miejskie, podmiejskie i regionalne** (art. 3a ustawy o transporcie kolejowym): Polregio, Koleje Mazowieckie, Śląskie, Dolnośląskie, Małopolskie, Wielkopolskie, SKM, WKD, ŁKA, Arriva RP | **Zmiana zakresu:** w spec. wpisałem Polregio jako przykład – błąd. Pokrywane: EIP, EIC, IC, TLK, IR (+ ŁKA Sprinter wg jednego źródła, niejasne) i połączenia międzynarodowe. Aplikacja powinna rozpoznać przewoźnika/kategorię i uczciwie powiedzieć "to nie przysługuje" |
| Brak odszkodowania: opóźnienie znane przed zakupem; rezygnacja ze zwrotu zamiast; **siła wyższa** (np. ekstremalna pogoda, osoby na torach, kradzież kabli, sabotaż); strajk personelu kolejowego **nie** jest siłą wyższą | Pytanie o przyczynę w UI + ostrzeżenie w wyniku i w piśmie |
| Odszkodowanie domyślnie często jako **bon**; gotówka na wniosek (przelew/przekaz) | Pismo musi wprost żądać wypłaty gotówką |
| Wypłata w ciągu 1 miesiąca od wniosku | Pole "przypomnienie o terminie" (twist: data, po której można eskalować) |
| Skarga do przewoźnika, potem przy odmowie/braku odpowiedzi do organu (w PL: Prezes UTK; mediacja Rzecznik Praw Pasażera Kolei). Terminy "3 miesiące" wg europa.eu | Etap "Odrzucona" → podpowiedź eskalacji do UTK |
| Przewoźnik musi przyjąć **unijny wspólny formularz** (rozp. wykonawcze 2024/949); PKP IC ma własny formularz; wniosek online / poczta / kasa | Pismo zawiera pola z formularza (data, numer biletu, wniosek o gotówkę, czy już składano wniosek); nie wymyślamy własnych wymagań |
| Termin złożenia wniosku: **niejednoznaczne źródła** (12 mies. / 2 mies. / 3 mies.) | W UI pokazujemy ostrożnie: "złóż jak najszybciej, nie później niż w ciągu 3 miesięcy" + link do przewoźnika; nie twierdzimy pewności |

## 2. Gemini API (wpływa na implementację)

- Dokumentacja przeniesiona na `aistudio.google.com/docs`; pojawiło się nowe "Interactions API", ale `POST /v1beta/models/{model}:generateContent` jest nadal opisany jako aktualny.
- Plan: `generateContent` z `inlineData` (base64) i `generationConfig.responseMimeType = application/json` + `responseJsonSchema`.
- Modele z darmowym poziomem i obsługą obrazu: **`gemini-3.5-flash-lite`** (wybór domyślny: tani, szybki, stabilny), `gemini-3.1-flash-lite`, `gemini-3.6-flash`. `gemini-2.5-flash` jest wycofywany (przekierowanie od 1.12.2026) – nie używać.
- Limity darmowe widoczne w konsoli AI Studio; **nie są jawnie opublikowane w dokumentacji** → traktować jako ograniczone, dodać obsługę 429 (komunikat + tryb ręczny).
- **Prywatność:** w darmowym poziomie treści mogą być używane do ulepszania produktów Google; w EOG/UK/CH obowiązują ograniczenia darmowego poziomu. → Ostrzeżenie w UI przy uploadzie ("nie wgrywaj danych, których nie chcesz wysyłać do Google"), zalecenie zasłonięcia danych osobowych; nie zapisujemy zdjęć.
- Model wymaga weryfikacji w czasie budowy (nazwy modeli szybko się zmieniają) → ID w konfiguracji, nie w kodzie.
- Pola `minLength/maxLength` w schemacie są ignorowane przy generowaniu → walidacja po stronie serwera (już w planie).

## 3. Zmiany do wprowadzenia w specyfikacji i planie

1. **Zakres/logika:** `Carrier` + kategoria pociągu → `EligibilityService` (kwalifikuje: IC/EIC/EIP/TLK/IR i międzynarodowe; wyklucza regionalne). Wynik: `Eligible | NotCovered | Unknown`.
2. **Kalkulator:** `MinimumPayout` = 16 zł domyślnie dla PKP IC; wynik zawsze "cena zapłacona".
3. **Nowe pola sprawy:** `TrainCategory`, `DelayCause` (nieznana / pogoda / strajk / awaria…), `PreferCash` (domyślnie true), `FiledAt`, `ResponseDueDate` (= FiledAt + 1 mies.).
4. **Pismo:** wprost żąda gotówki, powołuje art. 19 rozp. 2021/782, zawiera numer biletu, trasę, opóźnienie i kwotę; ostrzeżenie o sile wyższej tylko w UI.
5. **UI:** czytelny werdykt (Należy się / Nie należy się / Do sprawdzenia) z krótkim uzasadnieniem; ostrzeżenie o prywatności przy uploadzie; ostrożny komunikat o terminie.
6. **Gemini:** model `gemini-3.5-flash-lite` w configu; obsługa 429/5xx → komunikat i tryb ręczny.
7. **Do usunięcia/ostrożnie:** stałe "4 EUR ≈ 17 zł", przykład Polregio w seedach (zastąpić IC/EIC; jedna sprawa regionalna jako przykład "nie przysługuje").
8. **Demo:** nagrywać z prawdziwym (zanonimizowanym) lub syntetycznym biletem; w seedach tylko dane fikcyjne.
