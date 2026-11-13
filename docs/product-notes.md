# Uwagi produktowe i kierunki rozwoju

Notatki autora do README (sekcja "Kierunki rozwoju / model biznesowy").

## Limity odczytu biletu przez AI

- W MVP gość może odczytać bilet przez AI maksymalnie **5 razy na minutę** (limit per adres IP, konfigurowalny `RateLimiting:ExtractionsPerMinute`). Chroni to darmowy limit Gemini przed nadużyciem.
- Przy komercjalizacji liczba odczytów zależałaby od poziomu konta: np. mały limit dla darmowych użytkowników, wyższy (lub bez limitu) dla płatnych planów.

## Model biznesowy – wniosek autora

- Jako **samodzielna aplikacja** z abonamentem Refund Pilot byłby trudny do monetyzacji: reklamacje za opóźnienia zdarzają się rzadko (np. 1–2 razy w roku), więc konsument raczej nie zapłaci miesięcznego abonamentu za coś, z czego korzysta sporadycznie.
- Lepiej sprawdziłby się jako **moduł (podfunkcja) większego systemu** – np. aplikacji przewoźnika, wyszukiwarki połączeń, planera podróży, aplikacji biletowej lub bankowej – gdzie wartość dla pasażera rośnie, a koszt jest rozłożony na cały produkt.
- Jeśli jednak jako samodzielny produkt: oprócz abonamentu **płatność jednorazowa** za pojedynczą reklamację, w cenie minimalnie niższej niż miesięczny abonament (abonament opłaca się przy częstszym korzystaniu, jednorazowa płatność przy sporadycznym).
