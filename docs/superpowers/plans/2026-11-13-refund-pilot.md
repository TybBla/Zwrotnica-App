# Refund Pilot Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Full-stack MVP: a 3-step wizard turns a ticket (photo or manual entry) plus delay minutes into an honest verdict, a computed compensation and a ready complaint letter (text or PDF), with a CRUD list of cases that tracks the carrier's response deadline.

**Architecture:** ASP.NET Core API in layers (Domain → Application → Infrastructure → Api), SQLite via EF Core. Gemini (behind `IAiClient`) reads the ticket and writes the letter; pure services (`EligibilityService`, `CompensationCalculator`) decide and compute. React + Vite + TS wizard UI with light/dark themes.

**Tech Stack:** .NET 10 (SDK 10.0.201), EF Core SQLite, xUnit, QuestPDF, React + Vite + TypeScript (Node 26), plain CSS tokens, Gemini `generateContent` REST.

**Spec:** `docs/superpowers/specs/2026-11-13-refund-pilot-design.md` (also `docs/research.md`, `docs/interview-decisions.md`; `instructions.md` rules apply to all code)

## Global Constraints

- Deadline 2026-11-15 23:59; public GitHub repo; folders `backend/`, `frontend/`, `docs/`; `README.md`, `instructions.md`, demo `.gif`/`.mp4` committed.
- SQLite file only (gitignored). `GEMINI_API_KEY` optional; without it: manual path, template letter, ticket upload disabled with a Polish message.
- Rules in code only: delay 60–119 min = 25 %, ≥120 min = 50 %, cancellation = 100 % refund, price = amount actually paid, minimum payout default **16 zł**; regional/urban/suburban Polish carriers = `NotCovered`.
- Delay cause is optional and never blocks submission or changes the amount.
- Personal profile (name, address, IBAN, last payout choice) lives only in browser localStorage (device-bound, no accounts, optional JSON export/import); sent to the server only transiently for letter rendering; never sent to AI, never stored. Ticket images are never persisted.
- Payout form (cash / voucher) is the user's choice, preselected from the last choice saved in the profile; the letter reflects it. Cause never changes the amount; weather / persons-on-track set `RefusalRisk`; season ticket → verdict `Unknown`.
- Mobile-first: all flows fully usable at 375 px, touch targets ≥ 44 px, camera capture for the ticket photo, sticky primary action bar on small screens.
- Thin controllers, services in Application, DTOs in/out, no queries in loops, transactions for multi-table writes.
- REST under `/api`, ProblemDetails, Swagger, CORS `http://localhost:5173`. UI Polish; code/commits English (Conventional Commits).
- Gemini model id from config (`Gemini:Model`, default `gemini-3.5-flash-lite`); verify availability/free tier at build time. README states demo uses free Gemini and production would use a commercial model with proper privacy configuration.
- UI: bardzo ładny, nowoczesny; light fintech + dark glass themes with toggle; WCAG-minded contrast; tooltip ⓘ works on hover, keyboard focus and tap; loading/empty/error states; responsive to 375 px.

## Review Focus

- Gemini returns invalid JSON, 429 or 5xx → 502 ProblemDetails with Polish message; UI offers manual entry (no 500, no crash).
- Unknown carrier or category → verdict `Unknown` ("Do sprawdzenia"), never a guessed amount; regional carrier → `NotCovered` with explanation, 0 zł.
- Missing/zero ticket price → "podaj cenę" state instead of showing 0 zł compensation; amount below 16 zł → shown with "poniżej progu" flag.
- Upload not an image / over 5 MB / more than 4 files / empty request → 400 with clear message.
- Letter rendered without a profile keeps readable placeholders; profile values containing `{{`, newlines or very long text cannot break the letter or PDF; Polish diacritics render correctly in PDF.

---

## File Structure

```
backend/RefundPilot.sln
  src/RefundPilot.Domain/          RefundCase.cs, Enums.cs, CompensationCalculator.cs, EligibilityService.cs
  src/RefundPilot.Application/     Dtos/, Abstractions/, Services/{Case,Assessment,Extraction,Complaint}Service.cs, RuleBasedExtractor.cs, ComplaintTemplate.cs
  src/RefundPilot.Infrastructure/  AppDbContext.cs, CaseRepository.cs, SeedData.cs, Ai/{GeminiClient,NullAiClient}.cs, Pdf/ComplaintPdfRenderer.cs
  src/RefundPilot.Api/             Program.cs, Controllers/*
  tests/RefundPilot.Tests/         Domain/, Application/, Api/
frontend/src/{api,components,wizard,styles,hooks,profile}/
docs/  README.md  instructions.md  .env.example  .gitignore
```

### Task 1: Repository and solution scaffold

**Files:** Create `.gitignore`, `.env.example`, `backend/RefundPilot.sln`, four `src/` projects, `tests/RefundPilot.Tests` (xUnit); references Api→Infrastructure→Application→Domain, Tests→all.

**Interfaces:** Produces: buildable solution; namespaces `RefundPilot.{Domain,Application,Infrastructure,Api}`.

- [ ] **Step 1:** `git init`; `.gitignore` (bin/obj, `*.db`, `.env`, `node_modules`, `.agents/`, `skills-lock.json`, `.claude/`); `.env.example` with `GEMINI_API_KEY=`.
- [ ] **Step 2:** Create solution/projects via `dotnet new`, add references; add EF Core SQLite to Infrastructure.
- [ ] **Step 3:** Run `dotnet build backend/RefundPilot.sln` – Expected: Build succeeded.
- [ ] **Step 4:** Commit `chore: scaffold backend solution and repo config` (include `instructions.md`, `docs/`).

### Task 2: Domain – eligibility and compensation

**Files:** Create `Domain/Enums.cs`, `Domain/EligibilityService.cs`, `Domain/CompensationCalculator.cs`; Tests `Domain/EligibilityServiceTests.cs`, `Domain/CompensationCalculatorTests.cs`.

**Interfaces:** Produces:
```csharp
public enum IncidentType { Delay, Cancellation }
public enum CaseStatus { New, Sent, Paid, Rejected }
public enum Verdict { Eligible, NotCovered, Unknown }
public enum DelayCause { Unknown, Weather, PersonsOnTrack, RailStrike, Technical, Other }
public enum PayoutPreference { Cash, Voucher }
public sealed record EligibilityResult(Verdict Verdict, string Reason);
public sealed class EligibilityService { public EligibilityResult Assess(string? carrier, string? trainCategory); }
public sealed record CompensationOptions(decimal MinimumPayout = 16m);
public sealed record CompensationResult(decimal Amount, int Percent, bool BelowMinimum, bool PriceRequired, string Reason);
public sealed class CompensationCalculator(CompensationOptions options)
{ public CompensationResult Calculate(IncidentType type, int delayMinutes, decimal ticketPrice); }
```

- [ ] **Step 1:** Re-verify rule details against official sources (UTK, europa.eu, EUR-Lex 2021/782 art. 19) and note findings in `docs/research.md`; keep the 16 zł default unless a primary source says otherwise.
- [ ] **Step 2:** Failing tests (`[Theory]`): eligibility – "PKP Intercity"+"EIC"/"IC"/"TLK"/"IR"/"EIP" → Eligible; "Polregio", "Koleje Mazowieckie", "SKM", "WKD" (case/diacritic-insensitive) → NotCovered with Polish reason; unknown carrier or null → Unknown. Calculator – 59 → 0 %, 60 → 25 % (price 40 → 10.00), 119 → 25 %, 120 → 50 % (40 → 20.00), cancellation → 100 % (40 → 40.00), negative delay → 0 %, price ≤ 0 → `PriceRequired`, amount < 16 → `BelowMinimum == true` with amount kept.
- [ ] **Step 3:** Run `dotnet test --filter "FullyQualifiedName~Domain"` – Expected: FAIL.
- [ ] **Step 4:** Implement (pure; `decimal`, `MidpointRounding.AwayFromZero`, 2 dp; carrier match normalizes case/diacritics; Polish `Reason` strings).
- [ ] **Step 5:** Tests PASS. Commit `feat(domain): add eligibility and compensation rules`.

### Task 3: Persistence – entity, DbContext, seed

**Files:** Create `Domain/RefundCase.cs`, `Application/Abstractions/ICaseRepository.cs`, `Infrastructure/{AppDbContext,CaseRepository,SeedData}.cs`; Test `Application/CaseRepositoryTests.cs` (SQLite in-memory).

**Interfaces:** Produces:
```csharp
public sealed class RefundCase { int Id; string Carrier; string? TrainCategory; string FromStation; string ToStation;
  string? TicketNumber; DateOnly TravelDate; decimal TicketPrice; IncidentType IncidentType; int DelayMinutes;
  DelayCause? DelayCause; bool IsSeasonTicket; Verdict Verdict; bool RefusalRisk; decimal CompensationAmount;
  PayoutPreference PayoutPreference; CaseStatus Status;
  DateTime? FiledAt; DateTime? ResponseDueDate; string? Notes; DateTime CreatedAt; }
public interface ICaseRepository { Task<List<RefundCase>> ListAsync(); Task<RefundCase?> GetAsync(int id);
  Task AddAsync(RefundCase c); Task UpdateAsync(RefundCase c); Task<bool> DeleteAsync(int id); }
```
`SeedData.EnsureSeededAsync(AppDbContext)`: 5 fictional cases (IC 95 min, EIC 140 min, cancellation, one `Paid`, one regional `NotCovered`), single `AddRange` + `SaveChanges`.

- [ ] **Step 1:** Failing tests: add→list; delete true then false; seeding twice yields exactly 5 rows.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement (enums stored as strings, decimal precision, `EnsureCreated` on startup). **Step 4:** PASS. Commit `feat(infra): add EF Core SQLite persistence and seed`.

### Task 4: Assessment, cases CRUD, stats, API plumbing

**Files:** Create `Application/Dtos/*` (`CaseDto`, `CreateCaseRequest`, `UpdateCaseRequest`, `UpdateStatusRequest`, `AssessmentRequest`, `AssessmentDto`, `StatsDto`), `Application/Services/{CaseService,AssessmentService}.cs`, controllers `Cases`, `Assessments`, `Stats`, `Api/Program.cs`; Test `Api/CasesApiTests.cs` (`WebApplicationFactory`, in-memory SQLite).

**Interfaces:** Consumes Tasks 2–3. Produces:
```csharp
public interface IAssessmentService { AssessmentDto Assess(AssessmentRequest r); }  // Verdict, Reason, Amount, Percent, BelowMinimum, PriceRequired, RefusalRisk (true when cause is Weather or PersonsOnTrack)
// AssessmentRequest: Carrier, TrainCategory, IncidentType, DelayMinutes, TicketPrice, DelayCause?, IsSeasonTicket
public interface ICaseService { Task<IReadOnlyList<CaseDto>> ListAsync(); Task<CaseDto?> GetAsync(int id);
  Task<CaseDto> CreateAsync(CreateCaseRequest r); Task<CaseDto?> UpdateAsync(int id, UpdateCaseRequest r);
  Task<CaseDto?> SetStatusAsync(int id, CaseStatus s); Task<bool> DeleteAsync(int id); Task<StatsDto> GetStatsAsync(); }
// StatsDto(decimal Recovered, decimal Pending, int TotalCases); Recovered = sum Paid, Pending = New+Sent
```
Endpoints: `GET/POST /api/cases`, `GET/PUT/DELETE /api/cases/{id}`, `PATCH /api/cases/{id}/status`, `POST /api/assessments`, `GET /api/stats`. Setting status `Sent` sets `FiledAt = now` and `ResponseDueDate = FiledAt + 1 month` (clock injected via `TimeProvider`). Verdict/amount always recomputed server-side from request fields.

- [ ] **Step 1:** Failing API tests: POST case (IC, 95 min, price 40) → 201 + `Location` + amount 10.00 + verdict Eligible; regional carrier → verdict NotCovered, amount 0; empty carrier or price < 0 → 400 ProblemDetails; unknown id → 404; PATCH `Sent` sets `responseDueDate` one month ahead (fake `TimeProvider`); PATCH `Paid` increases `stats.recovered`; DELETE → 204; assessment with price 0 → `priceRequired: true`; delay cause omitted does not change the amount; cause `Weather` keeps verdict Eligible and amount identical but sets `refusalRisk: true`, `RailStrike` leaves it false; `isSeasonTicket: true` → verdict Unknown, amount 0.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement (DataAnnotations validation, thin controllers, `Program.cs`: DI, CORS, Swagger, ProblemDetails, global exception handler, seed). **Step 4:** `dotnet test` PASS. Commit `feat(api): cases CRUD, assessments, stats`.

### Task 5: AI client and ticket extraction

**Files:** Create `Application/Abstractions/IAiClient.cs`, `Application/RuleBasedExtractor.cs`, `Application/Services/ExtractionService.cs`, `Application/Dtos/ExtractionDto.cs`, `Infrastructure/Ai/{GeminiClient,NullAiClient}.cs`, `Api/Controllers/ExtractionsController.cs`; Tests `ExtractionServiceTests.cs`, `RuleBasedExtractorTests.cs`, `ExtractionsApiTests.cs`.

**Interfaces:** Produces:
```csharp
public sealed record AiImage(string MimeType, byte[] Data);
public interface IAiClient { bool IsConfigured { get; }
  Task<string> GenerateJsonAsync(string prompt, IReadOnlyList<AiImage> images, string jsonSchema, CancellationToken ct);
  Task<string> GenerateTextAsync(string prompt, CancellationToken ct); }
public sealed record ExtractionDto(string? Carrier, string? TrainCategory, string? FromStation, string? ToStation,
  string? TicketNumber, decimal? TicketPrice, DateOnly? TravelDate, IReadOnlyList<string> MissingFields, bool UsedAi);
public interface IExtractionService { Task<ExtractionDto> ExtractAsync(IReadOnlyList<AiImage> images, CancellationToken ct); }
public sealed class AiUnavailableException(string message) : Exception(message);
```
`POST /api/extractions` (multipart `files`): 1–4 image files, ≤ 5 MB each, else 400; not configured → 409 with Polish "AI wyłączone – wpisz dane ręcznie"; `AiUnavailableException` → 502.

- [ ] **Step 1:** Failing tests with fake `IAiClient`: valid JSON → DTO with unknown fields listed in `MissingFields`; malformed JSON → `AiUnavailableException`; implausible values (price ≤ 0 or > 2000, date older than 2 years or in the future) set to null and listed as missing; API tests for limits, empty request, not-configured 409.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement. Gemini: `POST /v1beta/models/{model}:generateContent`, `x-goog-api-key` header, `inlineData` base64 parts, `generationConfig.responseMimeType=application/json` + `responseJsonSchema`; prompt: return null for anything not clearly visible, never guess; 429/5xx/timeouts → `AiUnavailableException`. `RuleBasedExtractor` is used only for free text in tests/fallback of letter facts (YAGNI: drop if unused after Task 6).
- [ ] **Step 4:** PASS; one manual real call with the user's free key and a fictional sample ticket. Commit `feat(ai): add Gemini ticket extraction`.

### Task 6: Complaint letter (text and PDF)

**Files:** Create `Application/Services/ComplaintService.cs`, `Application/ComplaintTemplate.cs`, `Application/Dtos/{ComplaintRequest,ComplaintDto}.cs`, `Infrastructure/Pdf/ComplaintPdfRenderer.cs`, `Api/Controllers/ComplaintsController.cs`; Tests `ComplaintServiceTests.cs`, `ComplaintPdfTests.cs`.

**Interfaces:** Consumes `IAiClient`, `ICaseRepository`. Produces:
```csharp
public sealed record UserProfile(string? FullName, string? Address, string? Iban);
public sealed record ComplaintRequest(UserProfile? Profile, PayoutPreference Payout);
public sealed record ComplaintDto(string Subject, string Body, bool UsedAi);
public interface IComplaintService { Task<ComplaintDto?> GenerateAsync(int caseId, UserProfile? profile, PayoutPreference payout, CancellationToken ct); }
public interface IComplaintPdfRenderer { byte[] Render(ComplaintDto letter); }
```
Endpoints: `POST /api/cases/{id}/complaint` → `ComplaintDto`; `POST /api/cases/{id}/complaint/pdf` → `application/pdf` download. AI prompt contains only case facts (no profile) and must keep tokens `{{FULL_NAME}}`, `{{ADDRESS}}`, `{{IBAN}}` and the supplied amount verbatim; server substitutes tokens (sanitized: single line, max length) or leaves `[imię i nazwisko]` etc.; letter demands the chosen payout form (Cash: bank transfer and explicit refusal of a voucher; Voucher: accepts a voucher), cites art. 19 rozp. 2021/782; tests assert both variants; AI error or no key → `ComplaintTemplate`.

- [ ] **Step 1:** Failing tests: body contains carrier, route, ticket number, delay and amount formatted `10,00 zł` from the stored case; profile tokens replaced; no profile → placeholders; profile value `"{{IBAN}}\nX"` cannot inject extra tokens/lines; AI exception → template with `UsedAi == false`; unknown case → null; PDF bytes start with `%PDF` and the renderer does not throw on Polish diacritics (`ąęśżźćńół`).
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement (verify QuestPDF Community license terms and add the one-line license setting; embed a font supporting Polish glyphs). **Step 4:** `dotnet test` PASS. Commit `feat: complaint letter as text and PDF`.

### Task 7: Frontend scaffold, design system, themes

**Files:** Create `frontend/` (Vite React-TS), `src/api/{client,types}.ts`, `src/styles/{tokens,base}.css`, `src/App.tsx`, `src/components/{ThemeToggle,Tooltip}.tsx`, `vite.config.ts` (proxy `/api` → backend).

**Interfaces:** Produces `api/client.ts`: `extractTicket(files)`, `assess(r)`, `listCases()`, `createCase(r)`, `updateCase(id,r)`, `setStatus(id,s)`, `deleteCase(id)`, `getStats()`, `generateComplaint(id, profile?)`, `downloadComplaintPdf(id, profile?)`; types mirror backend DTOs; failures throw `ApiError(detail)`.

- [ ] **Step 1:** Scaffold with `npm create vite@latest` (TypeScript React); add Vitest + Testing Library.
- [ ] **Step 2:** Failing tests: `client` maps a ProblemDetails 502 to `ApiError` with server `detail`; `Tooltip` opens on hover, on keyboard focus, on tap and closes on Escape (role `tooltip`, `aria-describedby`).
- [ ] **Step 3:** Implement client, tokens for both themes (light fintech, dark glass) switched by `data-theme` + `prefers-color-scheme` + persisted toggle, app shell, tooltip. Direction from `apple-design`, `emil-design-eng`.
- [ ] **Step 4:** `npm run test` and `npm run build` pass. Commit `feat(web): scaffold, design system and themes`.

### Task 8: Wizard – ticket and delay

**Files:** Create `src/wizard/{Wizard,StepIndicator,StartChoice,TicketStep,DelayStep}.tsx`, `src/hooks/useWizard.ts`.

**Interfaces:** Consumes Task 7 client. Produces wizard state `{ source: 'upload'|'manual', ticket, delay: { incidentType, minutes, cause? }, assessment? }`.

- [ ] **Step 1:** Failing hook tests: choosing upload then extraction 502/409 switches to manual with an error message and keeps entered data; missing fields reported by extraction are highlighted and block "Dalej" only when carrier/route/price are empty; cause is optional (step valid without it); changing minutes re-calls `assess`.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement: start choice ("Wgraj bilet" / "Wpisz ręcznie"), drag-and-drop with local preview, AI-filled form for review, delay step with minutes input, "pociąg odwołany" toggle, optional cause select with ⓘ tooltip explaining why it is worth adding (force majeure may release the carrier; rail-staff strike does not), short unobtrusive note about the demo AI. **Step 4:** Tests PASS; walk the flow in the browser with and without API key. Commit `feat(web): wizard ticket and delay steps`.

### Task 9: Wizard – result, letter, profile

**Files:** Create `src/wizard/{ResultStep,VerdictCard,LetterChoice}.tsx`, `src/profile/{ProfilePanel,useProfile}.ts(x)`.

**Interfaces:** Consumes `assess`, `createCase`, `generateComplaint`, `downloadComplaintPdf`. Produces `useProfile(): { profile: UserProfile & { preferredPayout: PayoutPreference }; save(p): void; exportJson(): string; importJson(s: string): boolean }` (localStorage, try/catch; invalid import returns false).

- [ ] **Step 1:** Failing tests: `useProfile` survives unavailable localStorage; `VerdictCard` renders three verdict variants (Należy się / Nie należy się / Do sprawdzenia) and "poniżej progu"/"podaj cenę" states; for `NotCovered` the letter actions are hidden.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement result with animated amount, a payout choice (Gotówka / Bon, preselected from the profile's last choice and remembered on change), a `RefusalRisk` warning when set, saving the case, then "Wybierz format": **Tekst** (subject + body with copy buttons) or **PDF** (download). Profile panel (optional). **Step 4:** Tests PASS; manual walk-through. Commit `feat(web): result, letter formats and profile`.

### Task 10: Cases list with deadline countdown

**Files:** Create `src/components/{StatsHeader,CaseList,CaseCard,StatusSelect,DeadlineBadge,EmptyState}.tsx`, `src/hooks/useCases.ts`.

- [ ] **Step 1:** Failing tests for `DeadlineBadge`: status Sent with due in 18 days → "jeszcze 18 dni"; past due → escalation hint "Brak odpowiedzi? Możesz złożyć skargę do UTK"; non-Sent → nothing. Optimistic status update rolls back on API failure.
- [ ] **Step 2:** Run – FAIL. **Step 3:** Implement list, status chips, delete with confirmation, stats header (Odzyskano / Czeka), empty and error states. **Step 4:** PASS; `npm run build`. Commit `feat(web): cases list with deadline countdown`.

### Task 11: UI polish

- [ ] **Step 1:** Apply `animate`, `choreograph`, `mobile-native`, `break-ui`: step transitions, count-up amount, reduced-motion, worst-case data (long names, huge amounts, empty list), 375 px layout, touch targets ≥ 44 px, camera capture for the ticket photo (`accept="image/*"` + `capture`), sticky primary action bar on small screens, PDF download and text copy verified at phone width.
- [ ] **Step 2:** Review both themes in the browser (contrast of the letter text, glass panels), fix issues. Commit `style(web): motion, responsive and edge-case polish`.

### Task 12: README, demo, publication

**Files:** Create `README.md`, `docs/demo.gif` (or `.mp4`).

- [ ] **Step 1:** README: description, tech notes (AI vs rules split, fallback, privacy of profile, demo-vs-production AI note), step-by-step run (`dotnet run --project backend/src/RefundPilot.Api`, `cd frontend && npm install && npm run dev`), free Gemini key how-to, API table, how AI tools were used (Claude Code, `instructions.md`), legal disclaimer ("narzędzie pomocnicze, nie porada prawna").
- [ ] **Step 2:** Record demo (upload fictional ticket → delay → verdict → PDF/text → status Sent with countdown) with the browser tooling; embed in README.
- [ ] **Step 3:** Fresh-clone check in a temp directory following only the README, without API key. Expected: app loads, seed visible, manual flow and text letter work.
- [ ] **Step 4:** Publish: user creates the public GitHub repo (no `gh` installed) and pushes; user sends the email to recruitment@xeler.it (link + name).
- [ ] **Step 5 (stretch):** Free deploy – frontend Cloudflare Pages, backend Render; link in README.
