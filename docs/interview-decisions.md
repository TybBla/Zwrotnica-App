# Decyzje z wywiadu (log roboczy)

1. **Pociąg regionalny (Polregio, KM, SKM…):** aplikacja wydaje uczciwy werdykt "nie przysługuje" z krótkim uzasadnieniem (rozpoznanie przewoźnika/kategorii). Nie ukrywa takich biletów i nie pokazuje błędnej kwoty.
2. **Przyczyna opóźnienia:** pole w formularzu z ikonką informacyjną (ⓘ) obok etykiety. Opis (siła wyższa zwalnia przewoźnika z wypłaty, strajk personelu kolejowego nie) pojawia się po najechaniu/fokusie/tapnięciu, nie jest stale widoczny. Musi działać też na dotyk i z klawiatury.
4. **Dane osobowe w piśmie:** opcjonalny profil (imię, adres, konto) przechowywany wyłącznie w przeglądarce (localStorage), wstawiany do pisma po stronie klienta/serwera bez wysyłania do AI. Bez profilu pismo ma placeholdery.
5. **Kreator 3 kroków** (Bilet → Opóźnienie → Wynik). Na starcie wybór ścieżki: **wgraj bilet** (AI uzupełnia pola, użytkownik sprawdza i dopełnia braki) albo **wpisz ręcznie**.
6. **Opóźnienie:** bez wgrywania dowodu opóźnienia – użytkownik wpisuje liczbę minut (lub oznacza odwołanie). AI czyta tylko bilet. (Zmiana względem specyfikacji: usuwamy upload dowodu opóźnienia.)
7. **Przyczyna opóźnienia:** pole całkowicie opcjonalne (przewoźnik często jej nie podaje), z ikonką ⓘ wyjaśniającą, że warto ją podać; brak przyczyny nie blokuje ani nie zmienia werdyktu.
8. **Odbiór pisma (koniec kreatora):** użytkownik wybiera format. **Tekstowy** = temat e-maila + treść do skopiowania/wklejenia. **PDF** = pobranie przygotowanego pliku. Mój wybór techniczny: PDF generowany po stronie serwera (QuestPDF, endpoint przyjmujący finalny tekst pisma – profil użytkownika nie jest zapisywany na serwerze); weryfikacja licencji i polskich znaków przy budowie. Bez mailto.
9. **Styl wizualny:** dwa motywy z przełącznikiem – **jasny fintech** (biel, miękkie cienie, duże zaokrąglenia, jeden akcent) i **ciemny szklany** (ciemne tło, gradienty, półprzezroczyste panele). Oba muszą zachować czytelność tekstu pisma i kontrast.
10. **Odliczanie terminu odpowiedzi** (1 miesiąc od wysłania) i podpowiedź skargi do UTK po terminie – tak.
11. **Forma wypłaty:** wybór gotówka/bon przez użytkownika, zapamiętywany w profilu (przeglądarka) i zaznaczany domyślnie; pismo dopasowane do wyboru.
12. **Przyczyna a odszkodowanie:** nie zmienia kwoty, ale ustawia ostrzeżenie o ryzyku odmowy (pogoda, osoby na torach); bilet okresowy → "Do sprawdzenia".
13. **Mobile-first:** pełna wygoda na telefonie jako wymóg projektowy.
14. **ZMIANA (zastępuje 4 i część 11):** prawdziwe konta użytkowników zamiast profilu w przeglądarce; bez eksportu/importu JSON. Profil konta (imię, adres, rachunek, preferowana forma wypłaty) dostępny z każdego urządzenia. Moja decyzja: kreator działa też jako gość, zapis sprawy i pismo wymagają konta; konto demo w seedzie.
15. **ZMIANA:** pismo wskazuje preferowaną formę wypłaty, nie wyklucza innych.
16. **Źródło biletu:** telefon – aparat lub galeria; komputer – zdjęcie lub plik (także PDF e-biletu).
3. **Prywatność AI:** demo/MVP używa darmowego Gemini; w README zaznaczyć, że w produkcji użyty byłby model klasy komercyjnej z odpowiednią konfiguracją prywatności. W UI tylko krótka, nienachalna wzmianka (bez dużych ostrzeżeń).
