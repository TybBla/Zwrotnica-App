# Wdrożenie w chmurze (Azure App Service)

Instrukcja krok po kroku dla osoby, która pierwszy raz wdraża aplikację na Azure. Cel: Zwrotnica pod publicznym adresem `https://<nazwa-aplikacji>.azurewebsites.net`, działająca na telefonie i komputerze.

## Jak to jest zbudowane

- **Jedna aplikacja App Service (Linux)** serwuje i API, i zbudowany frontend. Frontend (`frontend/dist`) trafia do `wwwroot/` opublikowanego API. Wszystko działa pod jednym adresem, więc ciasteczko logowania działa bez CORS i bez kombinacji z `SameSite`.
- Ścieżki `/api/*` obsługuje API. Każda inna ścieżka bez rozszerzenia pliku (np. `/sprawy`, `/profil`) zwraca `index.html`, a dalej routing robi React. Nieznane `/api/...` nadal zwraca 404 w formacie ProblemDetails.
- **Baza SQLite i klucze szyfrujące** leżą w `/home/data/` (`zwrotnica.db` i `keys/`). Na App Service Linux katalog `/home` jest trwały: przetrwa restart i ponowne wdrożenie. Dzięki zapisanym kluczom zaszyfrowane numery rachunków (IBAN) da się odczytać po restarcie.
- Wdrożenie robi GitHub Actions: `.github/workflows/deploy-azure.yml` (ręcznie albo przy każdym pushu do `main`).
- Ustawienia produkcyjne są w `backend/src/RefundPilot.Api/appsettings.Production.json` (bez sekretów). Sekrety (klucz Gemini, hasło konta demo) ustawiasz w portalu Azure jako „Application settings”, nigdy w repozytorium.

## Koszt

- Plan **Free F1** jest darmowy (0 zł). Ma limity: ok. 60 minut CPU na dobę, 1 GB RAM, 1 GB miejsca, brak „Always on”, brak własnej domeny z certyfikatem. Na demo dla rekrutera wystarcza.
- Konto Azure Free wymaga karty płatniczej do weryfikacji tożsamości, ale plan F1 nic nie kosztuje. Przy zakładaniu zasobów pilnuj, żeby plan był **F1**, a nie domyślny płatny (np. B1 albo P0v3).
- **Gemini (darmowy poziom):** klucz jest bezpłatny, ale treści wysłane do darmowego API mogą służyć Google do ulepszania usług. Do demo wgrywaj bilety przykładowe albo z zasłoniętymi danymi osobowymi. Bez klucza aplikacja działa w trybie ręcznym.

## Krok 1. Konto Azure

1. Wejdź na <https://azure.microsoft.com/free> i załóż darmowe konto (konto Microsoft + weryfikacja telefonem i kartą).
2. Zaloguj się do portalu: <https://portal.azure.com>.

## Krok 2. Grupa zasobów i App Service

1. W portalu wyszukaj **App Services** → **Create** → **Web App**.
2. Zakładka **Basics**:
   - **Subscription:** Twoja subskrypcja (np. „Azure subscription 1”).
   - **Resource Group:** **Create new** → np. `zwrotnica-rg`. Grupa zasobów to „folder” na wszystko, co dotyczy aplikacji. Ułatwia późniejsze usunięcie.
   - **Name:** np. `zwrotnica-demo`. Nazwa musi być unikalna w całym Azure, bo staje się adresem: `https://zwrotnica-demo.azurewebsites.net`. Jeżeli Azure dopisze do adresu losowy sufiks (opcja „unique default hostname”), zapisz pełny adres, który pokaże.
   - **Publish:** **Code**.
   - **Runtime stack:** **.NET 10 (LTS)**.
   - **Operating System:** **Linux**.
   - **Region:** **West Europe** albo **Poland Central**.
   - **Pricing plan:** **Create new** (np. `zwrotnica-plan`) i w **Pricing plan** wybierz **Free F1**. Jeżeli F1 nie jest widoczny, kliknij „Explore pricing plans” i wybierz kategorię „Dev/Test”.
3. Pozostałe zakładki zostaw domyślne. W zakładce **Deployment** nie włączaj integracji z GitHubem (workflow jest już w repozytorium). Jeżeli jest opcja **Basic authentication**, ustaw **Enable** (potrzebne do pliku publish profile, krok 4).
4. **Review + create** → **Create**. Po minucie kliknij **Go to resource**.

**A jeśli na liście nie ma .NET 10?** Wybierz najnowszy dostępny .NET i wdrażaj w trybie *self-contained* (aplikacja zabiera własne środowisko .NET, paczka jest większa, ok. 100 MB). Zobacz [Tryb self-contained](#tryb-self-contained).

## Krok 3. Ustawienia aplikacji (Application settings)

W zasobie App Service: **Settings → Environment variables → App settings** → **+ Add** dla każdego wpisu, na końcu **Apply** (aplikacja się zrestartuje).

| Nazwa | Wartość | Po co |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Tryb produkcyjny (to też domyślna wartość, ale lepiej ustawić jawnie). |
| `GEMINI_API_KEY` | Twój klucz z <https://aistudio.google.com/apikey> | Odczyt biletu i pisanie pism przez AI. Opcjonalne. |
| `Seed__Enabled` | `true` | Tworzy konto demo z przykładowymi sprawami przy pierwszym starcie. |
| `Seed__DemoEmail` | np. `demo@zwrotnica.test` | Login konta demo. |
| `Seed__DemoPassword` | **wymyśl własne hasło** | Hasło konta demo. Min. 6 znaków, wielka i mała litera, cyfra i znak specjalny (np. `Pociag!` + kilka cyfr). Nie wpisuj go do repozytorium; podaj rekruterowi w mailu. |
| `RateLimiting__ExtractionsPerMinute` | np. `5` | Opcjonalne. Limit odczytów biletu przez AI na adres IP na minutę (domyślnie 5). |
| `PublicBaseUrl` | `https://<nazwa-aplikacji>.azurewebsites.net` | Adres aplikacji do linku „Sprawa w Zwrotnicy” w e-mailu z przypomnieniem. Bez niego link wskazuje `http://localhost:5173`. |
| `Email__Smtp__Host` | np. `smtp-relay.brevo.com` albo `smtp.gmail.com` | Opcjonalne. Serwer SMTP dla przypomnień e-mail. Bez niego wiadomości są tylko zapisywane jako pliki `.eml` w `/home/data/outbox`. |
| `Email__Smtp__Port` | `587` | Port z STARTTLS (domyślnie 587; port 465 nie jest obsługiwany). |
| `Email__Smtp__User`, `Email__Smtp__Password` | login i hasło SMTP | **Tylko w App settings**, nigdy w repozytorium. |
| `Email__From` | adres nadawcy | Musi być adresem zweryfikowanym u dostawcy SMTP. `Email__FromName` domyślnie `Zwrotnica`. |

Uwagi:

- Podwójne podkreślenie `__` w nazwie oznacza zagnieżdżenie w konfiguracji .NET (`Seed__DemoEmail` = `Seed:DemoEmail`). Działa też `Gemini__ApiKey` zamiast `GEMINI_API_KEY`.
- Konto demo powstaje tylko raz. Zmiana hasła w ustawieniach później nie zmieni hasła istniejącego konta. Żeby zacząć od nowa, usuń bazę (sekcja [Rozwiązywanie problemów](#rozwiązywanie-problemów)).
- Opcjonalnie możesz nadpisać ścieżki: `ConnectionStrings__Default` (domyślnie `DataSource=/home/data/zwrotnica.db`) i `DataProtection__KeysPath` (domyślnie `/home/data/keys`).

Przypomnienia e-mail (opcjonalnie):

- **Brevo (darmowy plan):** załóż konto, zweryfikuj adres nadawcy, w **SMTP & API** wygeneruj klucz SMTP. Host `smtp-relay.brevo.com`, port `587`, użytkownik i hasło z tej strony.
- **Gmail:** włącz weryfikację dwuetapową i utwórz **hasło aplikacji**. Host `smtp.gmail.com`, port `587`, użytkownik = adres Gmail, hasło = hasło aplikacji, `Email__From` = ten sam adres.
- Przypomnienie idzie raz na sprawę, tylko na adres właściciela konta, gdy minie termin odpowiedzi przewoźnika (status „Wysłana”). Wysyłkę wyłączysz ustawieniem `Reminders__Enabled` = `false`.
- Na planie F1 aplikacja zasypia bez ruchu, więc przypomnienia wychodzą przy starcie i co godzinę, **gdy aplikacja działa**. W produkcji wysyłkę uruchamiałby harmonogram (np. timer Azure Functions albo WebJob) zamiast usługi w tle.
- Konto demo ma adres `.test`, który nie istnieje: przypomnienie dla sprawy demo serwer SMTP odrzuci albo zwróci. To oczekiwane.

Dalej w **Settings → Configuration → General settings**:

- **HTTPS Only:** **On** (zwykle domyślnie włączone). Aplikacja w produkcji wysyła ciasteczko logowania tylko przez HTTPS.
- **Always on:** na F1 niedostępne. Aplikacja „zasypia” po ok. 20 minutach bez ruchu, a pierwsze wejście potem trwa 10–30 sekund (zimny start). To normalne na darmowym planie.
- **SCM Basic Auth Publishing Credentials:** **On** (wymagane do publish profile w kroku 4). W nowych aplikacjach jest **domyślnie wyłączone**. Przełącz na On, kliknij **Save**, a potem odśwież stronę (F5). Dopiero wtedy przycisk „Download publish profile” zacznie działać.

**Nie dodawaj** ustawienia `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. Aplikacja sama obsługuje nagłówki `X-Forwarded-*` (ufa tylko adresom prywatnym, z których łączy się front-end Azure). To ustawienie włącza wbudowany mechanizm .NET, który ufa każdemu nadawcy. Gdy jest ustawione, aplikacja wyłącza własną obsługę, żeby nie przetwarzać nagłówków dwa razy. Jeśli je widzisz na liście App settings, usuń je.

## Krok 4. Publish profile → sekret w GitHubie

1. Upewnij się, że „SCM Basic Auth Publishing Credentials” jest włączone i zapisane (krok 3), a strona odświeżona. W zasobie App Service, na stronie **Overview**, kliknij **Download publish profile**. Pobierze się plik `zwrotnica-demo.PublishSettings`. To jest hasło do wdrażania: nie commituj go i nie wysyłaj nikomu. Jeżeli przycisk jest nieaktywny, włącz „SCM Basic Auth Publishing Credentials” (krok 3) i odśwież stronę.
2. Na GitHubie wypchnij repozytorium (jeśli jeszcze go nie ma) i wejdź w **Settings → Secrets and variables → Actions**:
   - zakładka **Secrets** → **New repository secret**: nazwa `AZURE_WEBAPP_PUBLISH_PROFILE`, wartość: **cała zawartość** pobranego pliku (otwórz go w Notatniku, zaznacz wszystko, skopiuj),
   - zakładka **Variables** → **New repository variable**: nazwa `AZURE_WEBAPP_NAME`, wartość: nazwa aplikacji z kroku 2 (np. `zwrotnica-demo`). Może też być sekretem o tej samej nazwie.
3. Plik `.PublishSettings` możesz potem usunąć z dysku.

## Krok 5. Uruchomienie wdrożenia

1. Na GitHubie: **Actions → Deploy to Azure App Service → Run workflow** (gałąź `main`) → **Run workflow**. Workflow uruchamia się też sam przy każdym pushu do `main`.
2. Workflow:
   1. uruchamia lint i testy frontendu oraz build i testy backendu (błąd zatrzymuje wdrożenie),
   2. buduje frontend (`npm ci && npm run build` w `frontend/`),
   3. publikuje API (`dotnet publish backend/src/RefundPilot.Api -c Release -o publish -r linux-x64`),
   4. kopiuje `frontend/dist/*` do `publish/wwwroot/`,
   5. wdraża folder `publish` akcją `azure/webapps-deploy@v3`.
3. Zielony znaczek przy zadaniu = wdrożone (5–8 minut). Jeżeli sekrety nie są ustawione, workflow kończy się na zielono z adnotacją „Set AZURE_WEBAPP_NAME and AZURE_WEBAPP_PUBLISH_PROFILE”, bez wdrażania.

## Krok 6. Sprawdzenie

1. Otwórz `https://<nazwa-aplikacji>.azurewebsites.net/api/health`. Powinno być `{"status":"ok"}`. Pierwsze wejście może trwać do 30 sekund (zimny start).
2. Otwórz `https://<nazwa-aplikacji>.azurewebsites.net`. Powinna pojawić się strona główna Zwrotnicy.
3. Zaloguj się kontem demo (`Seed__DemoEmail` / `Seed__DemoPassword`) i otwórz **Moje sprawy**. Odśwież stronę na `/sprawy`: musi się załadować (to sprawdza obsługę tras SPA).
4. Sprawdź na telefonie (ten sam adres).
5. Wpisz adres do README w miejsce `https://<nazwa-aplikacji>.azurewebsites.net`.

## Rozwiązywanie problemów

- **Podgląd logów na żywo:** w zasobie **Monitoring → Log stream**. Jeśli nic nie widać, włącz **Monitoring → App Service logs → Application logging: File System** i zapisz. Wyjątki przy starcie (np. złe hasło demo) są tam widoczne.
- **„Application Error” / 500 zaraz po starcie:**
  - sprawdź w Log stream komunikat wyjątku,
  - `Demo user could not be created: ...` → hasło demo nie spełnia reguł (wielka i mała litera, cyfra, znak specjalny, min. 6 znaków). Popraw `Seed__DemoPassword`, **Apply**,
  - błąd o brakującym frameworku `Microsoft.AspNetCore.App 10.0` → stos nie ma .NET 10: przejdź na [tryb self-contained](#tryb-self-contained),
  - sprawdź w **Configuration → General settings**, czy **Stack** to **.NET** i wersja **10**.
- **Baza i ścieżki:** dane leżą w `/home/data/zwrotnica.db`. Podgląd plików: **Development Tools → Advanced Tools (Kudu) → Go** → **Bash** albo **File Manager** → `/home/data`. Żeby zacząć od czystej bazy (np. po zmianie schematu lub hasła demo): zatrzymaj aplikację (**Stop**), usuń `/home/data/zwrotnica.db*`, uruchom (**Start**). Nie usuwaj `/home/data/keys`, chyba że usuwasz też bazę (bez kluczy zapisane IBAN-y są nie do odczytania).
- **Pierwsze wejście trwa długo:** zimny start na F1 (brak „Always on”). Przed pokazaniem demo otwórz `/api/health` i poczekaj, aż odpowie.
- **Aplikacja przestała odpowiadać (403 „Site Disabled” / „quota exceeded”):** F1 ma ok. 60 minut CPU na dobę. Limit odnawia się następnego dnia. Do jednego dnia prezentacji można tymczasowo przełączyć plan na B1 (**Scale up**; płatny, ok. kilkudziesięciu zł/mies., rozliczany godzinowo) i potem wrócić na F1.
- **Logowanie nie działa (po zalogowaniu nadal „gość”):** wchodzisz przez `http://` zamiast `https://`. Włącz **HTTPS Only**.
- **Odczyt biletu nie działa:** brak lub zły `GEMINI_API_KEY`. Aplikacja pokazuje wtedy tryb ręczny. Po zmianie klucza kliknij **Apply** (restart).
- **Workflow nie wdraża (401 / „Publish profile is invalid”):** wygeneruj profil od nowa (**Overview → Reset publish profile**, potem **Download publish profile**) i podmień sekret `AZURE_WEBAPP_PUBLISH_PROFILE`. Sprawdź, czy „SCM Basic Auth Publishing Credentials” jest włączone.

## Tryb self-contained

Domyślnie workflow publikuje aplikację **zależną od frameworka** (mała paczka, .NET dostarcza Azure ze stosu „.NET 10”). App Service Linux ma .NET 10 (LTS), więc to jest wybór podstawowy.

Jeżeli w Twoim regionie lub planie nie da się wybrać .NET 10:

1. W **Configuration → General settings** ustaw najbliższy dostępny stos .NET i w polu **Startup Command** wpisz:
   `bash -c "cd /home/site/wwwroot && chmod +x RefundPilot.Api && ./RefundPilot.Api"`
   (aplikacja uruchomi się z własnym środowiskiem .NET, bez polecenia `dotnet`; `chmod` na wypadek, gdyby paczka zip zgubiła prawo wykonywania). Zapisz.
2. Na GitHubie: **Actions → Deploy to Azure App Service → Run workflow**, zaznacz **Bundle the .NET runtime** → **Run workflow**. Workflow wywoła `dotnet publish ... -r linux-x64 --self-contained true`.
3. Push do `main` publikuje w trybie domyślnym, więc w trybie self-contained wdrażaj ręcznie (albo zmień domyślną wartość `self_contained` w workflow na `true`).

## Usunięcie wszystkiego

1. Portal → **Resource groups** → `zwrotnica-rg` → **Delete resource group** → wpisz nazwę → **Delete**. Usuwa aplikację, plan i dane (bazę i klucze).
2. Na GitHubie usuń sekret `AZURE_WEBAPP_PUBLISH_PROFILE` i zmienną `AZURE_WEBAPP_NAME` (inaczej push do `main` będzie próbował wdrażać do nieistniejącej aplikacji).
3. Opcjonalnie usuń klucz Gemini w Google AI Studio.

## Bezpieczeństwo w skrócie

- W repozytorium nie ma żadnych sekretów. Klucz Gemini i hasło demo są tylko w ustawieniach App Service, a publish profile tylko w sekretach GitHuba.
- W produkcji: HTTPS (HSTS, przekierowanie, ciasteczko `Secure` + `HttpOnly` + `SameSite=Lax`), Swagger wyłączony, CORS niepotrzebny (jeden origin).
- Aplikacja stoi za front-endem Azure, więc czyta prawdziwy adres IP klienta i schemat `https` z nagłówków `X-Forwarded-For` / `X-Forwarded-Proto` (`ForwardedHeaders:Enabled` w `appsettings.Production.json`). Ufa tylko połączeniom z adresów prywatnych, CGNAT, link-local i loopback i bierze tylko jeden, ostatni wpis, dopisany przez Azure. Podrobiony wpis z lewej strony nagłówka niczego nie zmienia. Dzięki temu limit odczytów AI liczy się per użytkownik, a nie wspólnie dla wszystkich.
