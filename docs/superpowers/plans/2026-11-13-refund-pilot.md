# Refund Pilot Implementation Plan (v3 – accounts)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Full-stack MVP: a 3-step wizard turns a ticket (photo/file or manual entry) plus delay minutes into an honest verdict, a computed compensation and a ready complaint letter (text or PDF). Users have accounts (profile + their own cases with response-deadline tracking); the wizard also works for guests until saving.

**Architecture:** ASP.NET Core API in layers (Domain → Application → Infrastructure → Api), SQLite via EF Core, ASP.NET Core Identity (cookie auth). Gemini (behind `IAiClient`) reads the ticket and writes the letter; pure services (`EligibilityService`, `CompensationCalculator`) decide and compute. React + Vite + TS wizard UI with light/dark themes.

**Tech Stack:** .NET 10 (SDK 10.0.201), EF Core SQLite 10.x, ASP.NET Core Identity, xUnit, QuestPDF, Swashbuckle, React + Vite + TypeScript (Node 26), plain CSS tokens, Gemini `generateContent` REST.

**Spec:** `docs/superpowers/specs/2026-11-13-refund-pilot-design.md` (also `docs/research.md`, `docs/interview-decisions.md`; `instructions.md` rules apply to all code)

> Status note: Tasks 1–3 were executed under plan v2 (same numbering). v3 inserts Task 4 (accounts) and renumbers the rest.

## Global Constraints

- Deadline 2026-11-15 23:59; public GitHub repo; folders `backend/`, `frontend/`, `docs/`; `README.md`, `instructions.md`, demo `.gif`/`.mp4` committed.
- SQLite file only (gitignored). `GEMINI_API_KEY` optional; without it: manual path, template letter, ticket upload disabled with a Polish message.
- Rules in code only: delay 60–119 min = 25 %, ≥120 min = 50 %, cancellation = 100 % refund, price = amount actually paid, minimum payout default **16 zł**; regional/urban/suburban Polish carriers = `NotCovered`.
- Delay cause is optional and never blocks submission or changes the amount; weather / persons-on-track set `RefusalRisk`; season ticket → verdict `Unknown`.
- **Accounts:** ASP.NET Core Identity, email + password, cookie auth, API returns 401/403 (never redirects). Profile (FullName, Address, Iban, PreferredPayout) stored with the user; available on any device. `/api/cases*` and `/api/profile` require auth; a user only ever sees their own cases (another user's case → 404). Wizard extraction/assessment work anonymously; saving a case and the letter require login. Demo account seeded (credentials in `appsettings.Development.json` and README – test values only).
- Personal data never goes to the AI. Ticket files are never persisted. All profile fields except email/password are optional. **Iban is encrypted at rest** (ASP.NET Data Protection) and shown only to its owner; UI copy states exactly that (encrypted, visible only to you, never sent to AI) and nothing stronger.
- Letters list the placeholders left for the user to fill (e.g. "Uzupełnij w piśmie: numer rachunku, adres").
- Payout form (transfer / voucher) is the user's choice, preselected from the profile's `PreferredPayout` and saved back on change; the letter states the **preferred** form without excluding others.
- Ticket input: phone – camera **or** gallery; desktop – image or file; accepted types JPEG/PNG/WebP/HEIC/HEIF and PDF, 1–4 files, ≤ 5 MB each.
- Thin controllers, services in Application, DTOs in/out, no queries in loops, transactions for multi-table writes. SQLite stores decimal as TEXT → no server-side `Sum`/`OrderBy`/comparisons on decimal columns (aggregate in memory). DateTimes stored and returned as UTC.
- REST under `/api`, ProblemDetails, Swagger (Swashbuckle) in Development, CORS `http://localhost:5173`, no HTTPS redirection in dev, Api http profile on port 5000. UI Polish; code/commits English (Conventional Commits, trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- Gemini model id from config (`Gemini:Model`, default `gemini-3.5-flash-lite`). README: demo uses free Gemini; production would use a commercial model with proper privacy configuration; production would encrypt sensitive profile data (IBAN).
- UI: bardzo ładny, nowoczesny; light fintech + dark glass themes with toggle; WCAG-minded contrast; tooltip ⓘ works on hover, keyboard focus and tap; loading/empty/error states; **mobile-first**: fully usable at 375 px, touch targets ≥ 44 px, sticky primary action bar on small screens.

## Review Focus

- Gemini returns invalid JSON, 429 or 5xx → 502 ProblemDetails with Polish message; UI offers manual entry.
- User A requests user B's case (GET/PUT/PATCH/DELETE/complaint) → 404; anonymous request to `/api/cases` → 401 JSON, not a 302 redirect.
- Unknown carrier or category → verdict `Unknown`, never a guessed amount; regional carrier → `NotCovered`, 0 zł.
- Missing/zero ticket price → "podaj cenę" state; amount below 16 zł → shown with "poniżej progu" flag.
- Upload of wrong type / > 5 MB / > 4 files / empty → 400; profile fields with `{{`, newlines or very long text cannot break the letter or PDF; Polish diacritics render correctly in PDF.

---

## File Structure

```
backend/RefundPilot.sln
  src/RefundPilot.Domain/          RefundCase.cs, Enums.cs, CompensationCalculator.cs, EligibilityService.cs, UserProfile.cs
  src/RefundPilot.Application/     Dtos/, Abstractions/{ICaseRepository,ICurrentUser,IUserProfileStore,IAiClient}.cs, Services/{Case,Assessment,Extraction,Complaint,Profile}Service.cs, ComplaintTemplate.cs
  src/RefundPilot.Infrastructure/  AppDbContext.cs (IdentityDbContext<AppUser>), AppUser.cs, CaseRepository.cs, UserProfileStore.cs, SeedData.cs, Ai/{GeminiClient,NullAiClient}.cs, Pdf/ComplaintPdfRenderer.cs, DependencyInjection.cs
  src/RefundPilot.Api/             Program.cs, CurrentUser.cs, Controllers/*
  tests/RefundPilot.Tests/         Domain/, Application/, Api/
frontend/src/{api,auth,components,wizard,cases,profile,styles,hooks}/
```

### Task 1: Repository and solution scaffold — DONE (663652c)

### Task 2: Domain – eligibility and compensation — DONE (d238d1b)

Produces (in use): `IncidentType`, `CaseStatus`, `Verdict`, `DelayCause`, `PayoutPreference {Cash, Voucher}`, `EligibilityService.Assess(carrier, category) → EligibilityResult(Verdict, Reason)`, `CompensationCalculator(CompensationOptions(16m)).Calculate(type, minutes, price) → CompensationResult(Amount, Percent, BelowMinimum, PriceRequired, Reason)`.

### Task 3: Persistence – entity, DbContext, seed — DONE (6c1ec12, 808a4d7)

Produces (in use): `RefundCase` (Domain), `ICaseRepository` (ListAsync, GetAsync(id), AddAsync, UpdateAsync, DeleteAsync(id)), `AppDbContext`, `CaseRepository`, `SeedData.EnsureSeededAsync`, `AddInfrastructure(services, connectionString)`.

### Task 4: Accounts, profile, user-scoped cases

**Files:** Create `Domain/UserProfile.cs`, `Application/Abstractions/{ICurrentUser,IUserProfileStore}.cs`, `Application/Dtos/{ProfileDto,UpdateProfileRequest}.cs`, `Application/Services/ProfileService.cs`, `Infrastructure/{AppUser,UserProfileStore}.cs`, `Api/CurrentUser.cs`, `Api/Controllers/{ProfileController,AuthController}.cs`; Modify `Domain/RefundCase.cs` (+`UserId`), `Application/Abstractions/ICaseRepository.cs`, `Infrastructure/{AppDbContext,CaseRepository,SeedData,DependencyInjection}.cs`, `Api/Program.cs`, `Api/Properties/launchSettings.json`, `Api/appsettings.Development.json`; Tests `Application/CaseRepositoryTests.cs` (update), `Api/AuthApiTests.cs`, `Api/ProfileApiTests.cs`, `Api/TestApiFactory.cs`.

**Interfaces:** Produces:
```csharp
public sealed record UserProfile(string? FullName, string? Address, string? Iban, PayoutPreference PreferredPayout);
public interface ICurrentUser { string? UserId { get; } }
public interface IUserProfileStore { Task<UserProfile?> GetAsync(string userId); Task<bool> UpdateAsync(string userId, UserProfile p); }
public interface IProfileService { Task<ProfileDto?> GetAsync(); Task<ProfileDto?> UpdateAsync(UpdateProfileRequest r); }
// ProfileDto(string Email, string? FullName, string? Address, string? Iban, PayoutPreference PreferredPayout)
public interface ICaseRepository { Task<List<RefundCase>> ListAsync(string userId); Task<RefundCase?> GetAsync(int id, string userId);
  Task AddAsync(RefundCase c); Task UpdateAsync(RefundCase c); Task<bool> DeleteAsync(int id, string userId); }
```
Endpoints: Identity API mapped under `/api/auth` (`register`, `login?useCookies=true`, `manage/info`), `POST /api/auth/logout` (custom, 204), `GET/PUT /api/profile` (authorized). Cookie: `HttpOnly`, `SameSite=Lax`, 401/403 instead of redirects. Seed: demo user (email/password from `Seed:DemoEmail`/`Seed:DemoPassword` in `appsettings.Development.json`, e.g. `demo@refundpilot.test`) owning the 5 seeded cases, profile filled with fictional data. Wire `Program.cs`: `AddInfrastructure`, Identity, `EnsureCreated` + seed on startup, Swashbuckle in Development, CORS for `http://localhost:5173` with credentials, ProblemDetails + global exception handler, no `UseHttpsRedirection`; launchSettings http profile `http://localhost:5000`.

- [ ] **Step 1:** Failing tests: repository `ListAsync(userA)` excludes userB's cases, `GetAsync(id, wrongUser)` → null, `DeleteAsync(id, wrongUser)` → false; API (`WebApplicationFactory`, in-memory SQLite): register → login with cookies → `GET /api/profile` 200 with email; anonymous `GET /api/profile` → 401 (no 302 Location); `PUT /api/profile` with `preferredPayout: "Voucher"` persists; invalid IBAN-ish input over 34 chars or name over 200 chars → 400; logout → subsequent `GET /api/profile` 401; seeded demo user can log in and owns 5 cases.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement. **Step 4:** `dotnet test` PASS, output pristine; `dotnet run --project backend/src/RefundPilot.Api` starts on :5000 and `/swagger` loads. Commit `feat(auth): accounts, profile and user-scoped cases`.

### Task 5: Assessment, cases CRUD, stats

**Files:** Create `Application/Dtos/*` (`CaseDto`, `CreateCaseRequest`, `UpdateCaseRequest`, `UpdateStatusRequest`, `AssessmentRequest`, `AssessmentDto`, `StatsDto`), `Application/Services/{CaseService,AssessmentService}.cs`, controllers `Cases`, `Assessments`, `Stats`; Test `Api/CasesApiTests.cs`.

**Interfaces:** Consumes Tasks 2–4 (`ICurrentUser`, user-scoped repository). Produces:
```csharp
public interface IAssessmentService { AssessmentDto Assess(AssessmentRequest r); }
// AssessmentRequest: Carrier, TrainCategory, IncidentType, DelayMinutes, TicketPrice, DelayCause?, IsSeasonTicket
// AssessmentDto: Verdict, Reason, Amount, Percent, BelowMinimum, PriceRequired, RefusalRisk (Weather or PersonsOnTrack)
public interface ICaseService { Task<IReadOnlyList<CaseDto>> ListAsync(); Task<CaseDto?> GetAsync(int id);
  Task<CaseDto> CreateAsync(CreateCaseRequest r); Task<CaseDto?> UpdateAsync(int id, UpdateCaseRequest r);
  Task<CaseDto?> SetStatusAsync(int id, CaseStatus s); Task<bool> DeleteAsync(int id); Task<StatsDto> GetStatsAsync(); }
// StatsDto(decimal Recovered, decimal Pending, int TotalCases); Recovered = sum Paid, Pending = New+Sent (summed in memory)
```
Endpoints: `GET/POST /api/cases`, `GET/PUT/DELETE /api/cases/{id}`, `PATCH /api/cases/{id}/status`, `GET /api/stats` (authorized, current user only); `POST /api/assessments` (anonymous). Create/Update DTOs carry `DelayCause?`, `IsSeasonTicket`, `PayoutPreference`; verdict/amount/RefusalRisk always recomputed server-side. Create sets `UserId` from `ICurrentUser`. Update/SetStatus/Delete load via `GetAsync(id, userId)` first (missing → 404). Status `Sent` sets `FiledAt = now`, `ResponseDueDate = FiledAt + 1 month` (UTC, via `TimeProvider`).

- [ ] **Step 1:** Failing API tests: POST case (IC, 95 min, price 40) → 201 + `Location` + amount 10.00 + Eligible; regional → NotCovered, 0; empty carrier / price < 0 → 400 ProblemDetails; unknown or other user's id → 404; PATCH `Sent` sets `responseDueDate` one month ahead (fake `TimeProvider`); PATCH `Paid` raises `stats.recovered`; DELETE → 204; assessment price 0 → `priceRequired: true`; cause omitted does not change the amount; `Weather` → same amount, `refusalRisk: true`; `RailStrike` → false; `isSeasonTicket: true` → Unknown, 0; anonymous `POST /api/assessments` → 200; anonymous `GET /api/cases` → 401.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement. **Step 4:** `dotnet test` PASS. Commit `feat(api): cases CRUD, assessments, stats`.

### Task 6: AI client and ticket extraction

**Files:** Create `Application/Abstractions/IAiClient.cs`, `Application/Services/ExtractionService.cs`, `Application/Dtos/ExtractionDto.cs`, `Infrastructure/Ai/{GeminiClient,NullAiClient}.cs`, `Api/Controllers/ExtractionsController.cs`; Tests `ExtractionServiceTests.cs`, `ExtractionsApiTests.cs`.

**Interfaces:** Produces:
```csharp
public sealed record AiFile(string MimeType, byte[] Data);
public interface IAiClient { bool IsConfigured { get; }
  Task<string> GenerateJsonAsync(string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct);
  Task<string> GenerateTextAsync(string prompt, CancellationToken ct); }
public sealed record ExtractionDto(string? Carrier, string? TrainCategory, string? FromStation, string? ToStation,
  string? TicketNumber, decimal? TicketPrice, DateOnly? TravelDate, IReadOnlyList<string> MissingFields, bool UsedAi);
public interface IExtractionService { Task<ExtractionDto> ExtractAsync(IReadOnlyList<AiFile> files, CancellationToken ct); }
public sealed class AiUnavailableException(string message) : Exception(message);
```
`POST /api/extractions` (anonymous, multipart `files`): 1–4 files, types `image/jpeg|png|webp|heic|heif` or `application/pdf`, ≤ 5 MB each, else 400; not configured → 409 "AI wyłączone – wpisz dane ręcznie"; `AiUnavailableException` → 502. No `RuleBasedExtractor` (dropped – YAGNI).

- [ ] **Step 1:** Failing tests with fake `IAiClient`: valid JSON → DTO with unknown fields in `MissingFields`; malformed JSON → `AiUnavailableException`; implausible values (price ≤ 0 or > 2000, date older than 2 years or in the future) → null and listed missing; API: PDF accepted, `.txt` rejected, 6 MB rejected, 5 files rejected, empty rejected, not-configured 409.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement Gemini: `POST /v1beta/models/{model}:generateContent`, header `x-goog-api-key`, base64 `inlineData` parts (images and PDF), `generationConfig.responseMimeType=application/json` + `responseJsonSchema`; prompt: return null for anything not clearly visible, never guess; 429/5xx/timeouts → `AiUnavailableException`.
- [ ] **Step 4:** PASS; one manual real call with the user's free key and a fictional sample ticket (if key available). Commit `feat(ai): Gemini ticket extraction`.

### Task 7: Complaint letter (text and PDF)

**Files:** Modify `Infrastructure/UserProfileStore.cs` (encrypt/decrypt `Iban` with `IDataProtector`, purpose `"RefundPilot.Profile.Iban"`; existing plaintext seed value encrypted at seed time); Create `Application/Services/ComplaintService.cs`, `Application/ComplaintTemplate.cs`, `Application/Dtos/{ComplaintRequest,ComplaintDto}.cs`, `Infrastructure/Pdf/ComplaintPdfRenderer.cs`, `Api/Controllers/ComplaintsController.cs`; Tests `ComplaintServiceTests.cs`, `ComplaintPdfTests.cs`.

**Interfaces:** Consumes `IAiClient`, `ICaseRepository`, `IUserProfileStore`, `ICurrentUser`. Produces:
```csharp
public sealed record ComplaintRequest(PayoutPreference Payout, string? OneTimeIban, bool SaveIbanToProfile);
public sealed record ComplaintDto(string Subject, string Body, bool UsedAi, IReadOnlyList<string> MissingFields);
// MissingFields: Polish labels of placeholders left in the letter, e.g. ["numer rachunku", "adres"]; Iban required only for Payout=Cash
public interface IComplaintService { Task<ComplaintDto?> GenerateAsync(int caseId, PayoutPreference payout, CancellationToken ct); }
public interface IComplaintPdfRenderer { byte[] Render(ComplaintDto letter); }
```
Endpoints (authorized): `POST /api/cases/{id}/complaint` → `ComplaintDto`; `POST /api/cases/{id}/complaint/pdf` → `application/pdf` download (`Content-Disposition: attachment`). AI prompt contains only case facts (no profile) and must keep tokens `{{FULL_NAME}}`, `{{ADDRESS}}`, `{{IBAN}}` and the supplied amount verbatim; server substitutes tokens from the account profile (sanitized: single line, max length; empty → `[imię i nazwisko]` etc.). Letter states the **preferred** payout form (transfer to the given account or voucher) without excluding others; cites art. 19 rozp. 2021/782; AI error or no key → `ComplaintTemplate`. Generating a letter also saves `payout` as the case's `PayoutPreference` and the profile's `PreferredPayout`.

- [ ] **Step 1:** Failing tests: body contains carrier, route, ticket number, delay and amount `10,00 zł` from the stored case; profile tokens replaced; empty profile → placeholders; profile value `"{{IBAN}}\nX"` cannot inject tokens/lines; Cash vs Voucher wording differs and neither variant contains a refusal of the other form; AI exception → template, `UsedAi == false`; other user's case → null; PDF bytes start with `%PDF`, Polish diacritics (`ąęśżźćńół`) do not throw; `OneTimeIban` is used in the letter and saved to the profile only when `SaveIbanToProfile` is true; missing name/address/iban are reported in `MissingFields` (iban only for Cash) and appear as `[…]` placeholders; Iban is stored encrypted (raw DB value ≠ plaintext) and round-trips through `IUserProfileStore`.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement (verify QuestPDF Community license terms; embed a font with Polish glyphs). **Step 4:** PASS. Commit `feat: complaint letter as text and PDF`.

### Task 8: Frontend scaffold, design system, themes, API/auth client

**Files:** Create `frontend/` (Vite React-TS), `src/api/{client,types}.ts`, `src/auth/{AuthProvider,useAuth}.tsx`, `src/styles/{tokens,base}.css`, `src/App.tsx`, `src/components/{ThemeToggle,Tooltip}.tsx`, `vite.config.ts` (proxy `/api` → `http://localhost:5000`).

**Interfaces:** Produces `api/client.ts` (all `fetch` with `credentials: 'include'`): `register`, `login`, `logout`, `me`, `getProfile`, `updateProfile`, `extractTicket(files)`, `assess(r)`, `listCases()`, `createCase(r)`, `updateCase(id,r)`, `setStatus(id,s)`, `deleteCase(id)`, `getStats()`, `generateComplaint(id, payout)`, `downloadComplaintPdf(id, payout)`; types mirror backend DTOs; failures throw `ApiError(status, detail)`. `useAuth(): { user, login, register, logout, loading }`.

- [ ] **Step 1:** Scaffold with `npm create vite@latest` (TypeScript React); add Vitest + Testing Library.
- [ ] **Step 2:** Failing tests: `client` maps ProblemDetails 502 → `ApiError` with `detail` and 401 → `ApiError.status === 401`; `Tooltip` opens on hover, keyboard focus and tap, closes on Escape (role `tooltip`, `aria-describedby`).
- [ ] **Step 3:** Implement client, auth provider, tokens for both themes (light fintech, dark glass; `data-theme` + `prefers-color-scheme` + persisted toggle), app shell with header (logo, theme toggle, account menu), tooltip. Direction from `apple-design`, `emil-design-eng`.
- [ ] **Step 4:** `npm run test` and `npm run build` pass. Commit `feat(web): scaffold, design system, themes and api client`.

### Task 9: Wizard – ticket and delay

**Files:** Create `src/wizard/{Wizard,StepIndicator,StartChoice,TicketStep,TicketSourcePicker,DelayStep}.tsx`, `src/hooks/useWizard.ts`.

**Interfaces:** Consumes Task 8 client. Produces wizard state `{ source: 'upload'|'manual', ticket, delay: { incidentType, minutes, cause?, isSeasonTicket }, assessment? }`.

- [ ] **Step 1:** Failing tests: extraction 502/409 switches to manual with a message and keeps data; missing fields highlighted, "Dalej" blocked only when carrier/route/price are empty; cause optional; changing minutes re-calls `assess`; `TicketSourcePicker` renders a camera input (`capture="environment"`) and a gallery/file input (no `capture`) on touch devices and a single file/drag-drop input on desktop, all with `accept` = images + `application/pdf`.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement: start choice ("Wgraj bilet" / "Wpisz ręcznie"), source picker ("Zrób zdjęcie" / "Wybierz z galerii" on phone; drag-and-drop or "Wybierz plik" on desktop), local preview (PDF shows file name/icon), AI-filled review form, delay step (minutes, "pociąg odwołany", "bilet okresowy", optional cause with ⓘ tooltip), short unobtrusive demo-AI note. **Step 4:** Tests PASS; `npm run build`. Commit `feat(web): wizard ticket and delay steps`.

### Task 10: Wizard – result, save (with inline auth), letter

**Files:** Create `src/wizard/{ResultStep,VerdictCard,PayoutChoice,LetterChoice}.tsx`, `src/auth/{AuthPanel,LoginForm,RegisterForm}.tsx`.

- [ ] **Step 1:** Failing tests: `VerdictCard` renders Należy się / Nie należy się / Do sprawdzenia plus "poniżej progu", "podaj cenę" and `RefusalRisk` warning; NotCovered hides save/letter actions; `PayoutChoice` preselects the profile's `preferredPayout`; anonymous "Zapisz sprawę" shows `AuthPanel` and after successful login/registration saves the case without losing wizard state.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement result with animated amount, payout choice (Przelew / Bon), optional one-time account number field for Przelew (prefilled from profile, "zapamiętaj na koncie" checkbox, ⓘ security note), save, then "Wybierz format": **Tekst** (subject + body with copy buttons) or **PDF** (download); after generation a clear notice lists `MissingFields` ("Uzupełnij w piśmie: …"). **Step 4:** PASS; `npm run build`. Commit `feat(web): result, inline auth, letter formats`.

### Task 11: Account pages and cases list with deadline countdown

**Files:** Create `src/auth/{LoginPage,RegisterPage}.tsx`, `src/profile/ProfilePage.tsx`, `src/cases/{CasesPage,StatsHeader,CaseCard,StatusSelect,DeadlineBadge,EmptyState}.tsx`, `src/hooks/useCases.ts`; simple routing (hash or `react-router` – one dependency allowed).

- [ ] **Step 1:** Failing tests: `DeadlineBadge` – Sent, due in 18 days → "jeszcze 18 dni"; past due → "Brak odpowiedzi? Możesz złożyć skargę do UTK"; non-Sent → nothing. Optimistic status update rolls back on failure. Profile form saves and shows validation errors from ProblemDetails.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement login/register pages, profile page (name, address, IBAN, preferred payout – all optional; next to IBAN a short security note: encrypted in the database, visible only to you, never sent to AI, optional – can be entered per letter instead), cases list with status chips, delete confirmation, stats header (Odzyskano / Czeka), empty/error states, protected routes redirect to login. **Step 4:** PASS; `npm run build`. Commit `feat(web): account pages and cases list`.

### Task 12: UI polish and mobile pass

- [ ] **Step 1:** Apply `animate`, `choreograph`, `mobile-native`, `break-ui`: step transitions, count-up amount, reduced-motion, worst-case data (long names, huge amounts, empty list), 375 px layout, touch targets ≥ 44 px, sticky primary action bar on small screens, camera/gallery inputs on a phone-width viewport, PDF download and text copy at phone width.
- [ ] **Step 2:** Review both themes in the browser (letter text contrast, glass panels) and fix issues. Commit `style(web): motion, responsive and edge-case polish`.

### Task 13: README, demo, publication

**Files:** Create `README.md`, `docs/demo.gif` (or `.mp4`).

- [ ] **Step 1:** README: description, tech notes (AI vs rules split, fallback, accounts and privacy, demo-vs-production AI note), step-by-step run (`dotnet run --project backend/src/RefundPilot.Api`, `cd frontend && npm install && npm run dev`), demo account, optional free Gemini key how-to, API table, how AI tools were used (Claude Code, `instructions.md`, superpowers workflow), legal disclaimer ("narzędzie pomocnicze, nie porada prawna"), out-of-scope rights (assistance, refunds, rerouting).
- [ ] **Step 2:** Record demo (login → upload fictional ticket → delay → verdict → PDF/text → status Sent with countdown) with the browser tooling; embed in README.
- [ ] **Step 3:** Fresh-clone check in a temp directory following only the README, without API key.
- [ ] **Step 4:** Publish: user creates the public GitHub repo (no `gh` installed) and pushes; user sends the email to recruitment@xeler.it.
- [ ] **Step 5 (stretch):** Free deploy – frontend Cloudflare Pages, backend Render; link in README.
