using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;
using RefundPilot.Domain;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Application;

public sealed class ComplaintServiceTests : IAsyncLifetime
{
    private const string Anna = "user-anna";
    private const string Empty = "user-empty";
    private const string Other = "user-other";
    private const string Iban = "PL61109010140000071219812874";

    private sealed class FakeUser(string? id) : ICurrentUser
    {
        public string? UserId => id;
        public string? Email => "x@test.pl";
    }

    private sealed class FakeAi(string? json = null, Exception? fail = null) : IAiClient
    {
        public bool IsConfigured => true;
        public string? Prompt { get; private set; }

        public Task<string> GenerateJsonAsync(string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct)
        {
            Prompt = prompt;
            return fail is null ? Task.FromResult(json!) : throw fail;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct) => Task.FromResult("");
    }

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private UserProfileStore _store = null!;
    private CaseRepository _cases = null!;
    private int _annaCaseId;
    private int _otherCaseId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new UserProfileStore(_db, new EphemeralDataProtectionProvider());
        _cases = new CaseRepository(_db);

        foreach (var id in new[] { Anna, Empty, Other })
            _db.Users.Add(new AppUser { Id = id, UserName = id, Email = $"{id}@test.pl" });
        await _db.SaveChangesAsync();
        await _store.UpdateAsync(Anna, new UserProfile("Anna Kowalska", "ul. Lipowa 12/4, 80-001 Gdańsk", Iban,
            PayoutPreference.OriginalPayment));

        _annaCaseId = await AddCaseAsync(Anna);
        _otherCaseId = await AddCaseAsync(Other);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<int> AddCaseAsync(string userId, DelayCause? cause = null, bool noStations = false)
    {
        var c = new RefundCase
        {
            UserId = userId,
            Carrier = "PKP Intercity",
            TrainCategory = "IC",
            FromStation = noStations ? null : "Gdynia Główna",
            ToStation = noStations ? null : "Kraków Główny",
            TicketNumber = "IC-4821-0093",
            TravelDate = new DateOnly(2026, 11, 1),
            TicketPrice = 40m,
            IncidentType = IncidentType.Delay,
            DelayMinutes = 70,
            DelayCause = cause,
            Verdict = Verdict.Eligible,
            CompensationAmount = 10m,
            PayoutPreference = PayoutPreference.OriginalPayment,
            Status = CaseStatus.New,
            CreatedAt = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
        };
        await _cases.AddAsync(c);
        return c.Id;
    }

    private sealed class FakePdf : IComplaintPdfRenderer
    {
        public int Calls { get; private set; }
        public ComplaintDto? Last { get; private set; }

        public byte[] Render(ComplaintDto letter)
        {
            Calls++;
            Last = letter;
            return [1, 2, 3];
        }
    }

    /// <summary>Records profile writes made through the service (seeding in InitializeAsync goes to the inner store).</summary>
    private sealed class RecordingStore(IUserProfileStore inner, List<UserProfile> writes) : IUserProfileStore
    {
        public Task<UserProfile?> GetAsync(string userId) => inner.GetAsync(userId);

        public Task<bool> UpdateAsync(string userId, UserProfile profile)
        {
            writes.Add(profile);
            return inner.UpdateAsync(userId, profile);
        }
    }

    private readonly FakePdf _pdf = new();
    private readonly List<UserProfile> _profileWrites = [];

    private ComplaintService Service(IAiClient ai, string userId = Anna) =>
        new(_cases, new RecordingStore(_store, _profileWrites), new FakeUser(userId), ai, new EfUnitOfWork(_db), _pdf);

    private static FakeAi Failing() => new(fail: new AiUnavailableException("down"));

    private static ComplaintRequest Req(PayoutPreference p = PayoutPreference.OriginalPayment,
        string? iban = null, bool save = false) => new(p, iban, save);

    private static string ValidAiJson(
        string body = "{{FULL_NAME}}\n{{ADDRESS}}\n\nWniosek o odszkodowanie 10,00 zł na podstawie art. 19 rozporządzenia (UE) 2021/782.\n\n{{PAYOUT}}\n\nZ poważaniem\n{{FULL_NAME}}") =>
        JsonSerializer.Serialize(new { subject = "Wniosek o odszkodowanie", body });

    private async Task<ComplaintDto> GenerateAsync(IAiClient ai, ComplaintRequest request, string userId = Anna, int? caseId = null) =>
        (await Service(ai, userId).GenerateAsync(caseId ?? (userId == Anna ? _annaCaseId : _otherCaseId), request, CancellationToken.None)).Letter!;

    [Fact]
    public async Task Template_ContainsCaseFactsAndLegalBasis()
    {
        var letter = await GenerateAsync(Failing(), Req());

        Assert.False(letter.UsedAi);
        foreach (var expected in new[]
                 { "PKP Intercity", "Gdynia Główna", "Kraków Główny", "IC-4821-0093", "70 min", "10,00 zł", "40,00 zł",
                   "art. 19", "2021/782", "01.11.2026" })
            Assert.Contains(expected, letter.Body);
        Assert.False(string.IsNullOrWhiteSpace(letter.Subject));
    }

    [Fact]
    public async Task Template_MentionsDelayCauseWhenGiven()
    {
        var id = await AddCaseAsync(Anna, DelayCause.Weather);

        var letter = await GenerateAsync(Failing(), Req(), caseId: id);

        Assert.Contains("warunki pogodowe", letter.Body);
    }

    [Fact]
    public async Task Template_CancellationIsDescribedAsCancellation()
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.IncidentType = IncidentType.Cancellation;
        c.DelayMinutes = 0;
        await _cases.UpdateAsync(c);

        var letter = await GenerateAsync(Failing(), Req());

        Assert.Contains("odwołan", letter.Body);
        Assert.DoesNotContain("0 min", letter.Body);
    }

    private async Task MakeAnnaCaseCancellationAsync()
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.IncidentType = IncidentType.Cancellation;
        c.DelayMinutes = 0;
        c.CompensationAmount = 40m;
        await _cases.UpdateAsync(c);
    }

    [Theory]
    [InlineData(PayoutPreference.OriginalPayment)]
    [InlineData(PayoutPreference.BankTransfer)]
    [InlineData(PayoutPreference.Voucher)]
    public async Task Template_Cancellation_RequestsTicketRefundUnderArticle18(PayoutPreference payout)
    {
        await MakeAnnaCaseCancellationAsync();

        var letter = await GenerateAsync(Failing(), Req(payout));

        Assert.StartsWith("Wniosek o zwrot należności za bilet – odwołanie pociągu PKP Intercity", letter.Subject);
        Assert.Contains("art. 18 rozporządzenia (UE) 2021/782", letter.Body);
        Assert.Contains("zwrot należności za bilet w wysokości 40,00 zł", letter.Body);
        Assert.Contains("Proszę o zwrot należności", letter.Body);
        Assert.DoesNotContain("art. 19", letter.Body);
        Assert.DoesNotContain("odszkodowa", letter.Body);
        Assert.DoesNotContain("odszkodowa", letter.Subject);
    }

    [Fact]
    public async Task Template_Delay_RequestsCompensationUnderArticle19()
    {
        var letter = await GenerateAsync(Failing(), Req());

        Assert.StartsWith("Wniosek o wypłatę odszkodowania – opóźnienie pociągu", letter.Subject);
        Assert.Contains("odszkodowania na podstawie art. 19 rozporządzenia (UE) 2021/782", letter.Body);
        Assert.DoesNotContain("art. 18", letter.Body);
    }

    [Fact]
    public async Task AiPrompt_Cancellation_AsksForRefundUnderArticle18()
    {
        await MakeAnnaCaseCancellationAsync();
        var ai = new FakeAi(ValidAiJson());

        await GenerateAsync(ai, Req());

        Assert.Contains("art. 18", ai.Prompt);
        Assert.DoesNotContain("art. 19", ai.Prompt);
    }

    [Theory]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot 40,00 zł na podstawie art. 18 rozporządzenia (UE) 2021/782 {{PAYOUT}}", true)]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} odszkodowanie 40,00 zł na podstawie art. 19 rozporządzenia (UE) 2021/782 {{PAYOUT}}", false)]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot 40,00 zł, art. 18 i art. 19 rozporządzenia (UE) 2021/782 {{PAYOUT}}", false)]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot 40,00 zł zgodnie z rozporządzeniem (UE) 2021/782 {{PAYOUT}}", false)]
    public async Task AiOutput_Cancellation_MustCiteArticle18(string body, bool usedAi)
    {
        await MakeAnnaCaseCancellationAsync();

        var letter = await GenerateAsync(new FakeAi(ValidAiJson(body)), Req());

        Assert.Equal(usedAi, letter.UsedAi);
        Assert.Contains("art. 18", letter.Body);
    }

    [Fact]
    public async Task AiOutput_Delay_CitingArticle18Only_FallsBackToTemplate()
    {
        var body = "{{FULL_NAME}} {{ADDRESS}} odszkodowanie 10,00 zł, art. 18 rozporządzenia (UE) 2021/782 {{PAYOUT}}";

        var letter = await GenerateAsync(new FakeAi(ValidAiJson(body)), Req());

        Assert.False(letter.UsedAi);
        Assert.Contains("art. 19", letter.Body);
    }

    [Fact]
    public async Task ProfileTokensAreReplaced()
    {
        var letter = await GenerateAsync(Failing(), Req());

        Assert.Contains("Anna Kowalska", letter.Body);
        Assert.Contains("ul. Lipowa 12/4, 80-001 Gdańsk", letter.Body);
        Assert.DoesNotContain("{{", letter.Body);
        Assert.Empty(letter.MissingFields);
    }

    [Fact]
    public async Task EmptyProfile_UsesPlaceholdersAndReportsMissingFields()
    {
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(), Empty, id);

        Assert.Contains("[imię i nazwisko]", letter.Body);
        Assert.Contains("[adres]", letter.Body);
        Assert.Equal(["imię i nazwisko", "adres"], letter.MissingFields);
    }

    [Fact]
    public async Task BankTransferWithoutIban_ReportsAccountNumberAsMissing()
    {
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer), Empty, id);

        Assert.Contains("[numer rachunku]", letter.Body);
        Assert.Equal(["imię i nazwisko", "adres", "numer rachunku"], letter.MissingFields);
    }

    [Theory]
    [InlineData(PayoutPreference.OriginalPayment)]
    [InlineData(PayoutPreference.Voucher)]
    public async Task OtherPayouts_NeverAskForAccountNumber(PayoutPreference payout)
    {
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(payout), Empty, id);

        Assert.DoesNotContain("numer rachunku", letter.MissingFields);
        Assert.DoesNotContain("[numer rachunku]", letter.Body);
    }

    [Fact]
    public async Task ProfileValuesCannotInjectTokensOrLines()
    {
        await _store.UpdateAsync(Empty, new UserProfile("{{IBAN}}\nX", "ul. A\r\n{{ADDRESS}}", null,
            PayoutPreference.OriginalPayment));
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(), Empty, id);

        Assert.DoesNotContain("{{", letter.Body);
        Assert.DoesNotContain("}}", letter.Body);
        Assert.Contains("IBAN X", letter.Body);
        Assert.DoesNotContain("\nX", letter.Body);
        Assert.DoesNotContain("A\r", letter.Body);
    }

    [Fact]
    public async Task ProfileValuesAreTruncatedToMaxLength()
    {
        await _store.UpdateAsync(Empty, new UserProfile("N", new string('a', 400), null, PayoutPreference.OriginalPayment));
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(), Empty, id);

        Assert.Contains(new string('a', 300), letter.Body);
        Assert.DoesNotContain(new string('a', 301), letter.Body);
    }

    [Fact]
    public async Task ThreePayoutWordings_AreDistinctAndNonExclusive()
    {
        var original = (await GenerateAsync(Failing(), Req(PayoutPreference.OriginalPayment))).Body;
        var transfer = (await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer))).Body;
        var voucher = (await GenerateAsync(Failing(), Req(PayoutPreference.Voucher))).Body;

        Assert.Contains("Proszę o wypłatę odszkodowania na rachunek bankowy lub kartę, z których opłacono bilet.", original);
        Assert.Contains($"Proszę o wypłatę odszkodowania przelewem na rachunek nr {Iban}.", transfer);
        Assert.Contains("Proszę o wypłatę odszkodowania w formie bonu.", voucher);

        Assert.DoesNotContain("w formie bonu", original);
        Assert.DoesNotContain("przelewem", original);
        Assert.DoesNotContain("opłacono bilet", voucher);
        Assert.DoesNotContain("przelewem", voucher);
        Assert.DoesNotContain("bonu", transfer);
        foreach (var body in new[] { original, transfer, voucher })
        {
            Assert.DoesNotContain("wyłącznie", body);
            Assert.DoesNotContain("nie akceptuj", body);
            Assert.DoesNotContain("odmawiam", body);
        }
    }

    [Fact]
    public async Task ValidAiOutput_IsUsedAndPayoutSentenceInsertedByServer()
    {
        var ai = new FakeAi(ValidAiJson());

        var letter = await GenerateAsync(ai, Req(PayoutPreference.Voucher));

        Assert.True(letter.UsedAi);
        Assert.Equal("Wniosek o odszkodowanie", letter.Subject);
        Assert.Contains("Proszę o wypłatę odszkodowania w formie bonu.", letter.Body);
        Assert.Contains("Anna Kowalska", letter.Body);
        Assert.DoesNotContain("{{", letter.Body);
    }

    [Fact]
    public async Task AiPrompt_ContainsFactsButNoPersonalData()
    {
        var ai = new FakeAi(ValidAiJson());

        await GenerateAsync(ai, Req(PayoutPreference.BankTransfer, "PL00 1111 2222 3333"));

        Assert.Contains("10,00 zł", ai.Prompt);
        Assert.Contains("PKP Intercity", ai.Prompt);
        Assert.Contains("{{FULL_NAME}}", ai.Prompt);
        foreach (var secret in new[] { "Anna", "Kowalska", "Lipowa", Iban, "PL00" })
            Assert.DoesNotContain(secret, ai.Prompt);
    }

    [Theory]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} kwota 12,00 zł")]       // wrong amount
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 kwota 10,00 zł")]                  // no payout token
    [InlineData("{{ADDRESS}} art. 19 2021/782 {{PAYOUT}} kwota 10,00 zł")]                     // no name token
    [InlineData("{{FULL_NAME}} {{ADDRESS}} {{PAYOUT}} kwota 10,00 zł")]                        // no legal basis
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł {{IBAN}}")]    // IBAN token but not a transfer
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł {{SECRET}}")]  // unknown token
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł oraz dodatkowo 500,00 zł")] // extra sum
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł, cena 99 zł")]               // extra integer sum
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł. Konto PL61 1090 1014 0000 0712 1981 2874")] // IBAN-like
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł. Konto 61109010140000071219812874")] // bare 26-digit account
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł. Konto 61 1090 1014 0000 0712 1981 2874")] // spaced account
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł wyłącznie przelewem")]       // exclusive clause
    [InlineData("{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} 10,00 zł, nie przyjmę gotówki")]     // cash wording
    public async Task InvalidAiOutput_FallsBackToTemplate(string body)
    {
        var letter = await GenerateAsync(new FakeAi(ValidAiJson(body)), Req());

        Assert.False(letter.UsedAi);
        Assert.Contains("10,00 zł", letter.Body);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"subject":"","body":"x"}""")]
    [InlineData("[]")]
    public async Task MalformedAiOutput_FallsBackToTemplate(string raw)
    {
        var letter = await GenerateAsync(new FakeAi(raw), Req());

        Assert.False(letter.UsedAi);
    }

    [Fact]
    public async Task AiException_FallsBackToTemplate()
    {
        var letter = await GenerateAsync(new FakeAi(fail: new HttpRequestException("boom")), Req());

        Assert.False(letter.UsedAi);
    }

    [Fact]
    public async Task OtherUsersCase_ReturnsNull()
    {
        var result = await Service(Failing()).GenerateAsync(_otherCaseId, Req(), CancellationToken.None);

        Assert.Equal(ComplaintStatus.NotFound, result.Status);
        Assert.Null(result.Letter);
    }

    [Fact]
    public async Task UnknownCase_ReturnsNull() =>
        Assert.Equal(ComplaintStatus.NotFound, (await Service(Failing()).GenerateAsync(9999, Req(), CancellationToken.None)).Status);

    [Fact]
    public async Task OneTimeIban_UsedInLetterButNotSavedByDefault()
    {
        var id = await AddCaseAsync(Empty);

        var letter = await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer, " PL27114020040000300201355387 "), Empty, id);

        Assert.Contains("rachunek nr PL27114020040000300201355387.", letter.Body);
        Assert.DoesNotContain("numer rachunku", letter.MissingFields);
        Assert.Null((await _store.GetAsync(Empty))!.Iban);
    }

    [Fact]
    public async Task OneTimeIban_SavedEncryptedWhenRequested()
    {
        var id = await AddCaseAsync(Empty);

        await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer, "PL27114020040000300201355387", save: true), Empty, id);

        Assert.Equal("PL27114020040000300201355387", (await _store.GetAsync(Empty))!.Iban);
        var raw = await _db.Database.SqlQueryRaw<string>("SELECT Iban AS Value FROM AspNetUsers WHERE Id = {0}", Empty)
            .SingleAsync();
        Assert.NotEqual("PL27114020040000300201355387", raw);
    }

    [Fact]
    public async Task OneTimeIban_OverridesProfileIbanInLetterOnly()
    {
        var letter = await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer, "PL27114020040000300201355387"));

        Assert.Contains("PL27114020040000300201355387", letter.Body);
        Assert.DoesNotContain(Iban, letter.Body);
        Assert.Equal(Iban, (await _store.GetAsync(Anna))!.Iban);
    }

    [Fact]
    public async Task Generating_SavesPayoutOnCaseAndProfile()
    {
        await GenerateAsync(Failing(), Req(PayoutPreference.Voucher));

        Assert.Equal(PayoutPreference.Voucher, (await _cases.GetAsync(_annaCaseId, Anna))!.PayoutPreference);
        var profile = await _store.GetAsync(Anna);
        Assert.Equal(PayoutPreference.Voucher, profile!.PreferredPayout);
        Assert.Equal("Anna Kowalska", profile.FullName);
        Assert.Equal(Iban, profile.Iban);
    }

    [Fact]
    public async Task AiOutput_MayMentionTicketPriceButNoOtherSum()
    {
        var body = "{{FULL_NAME}} {{ADDRESS}} art. 19 2021/782 {{PAYOUT}} odszkodowanie 10,00 zł od ceny biletu 40,00 zł";

        var letter = await GenerateAsync(new FakeAi(ValidAiJson(body)), Req());

        Assert.True(letter.UsedAi);
    }

    [Fact]
    public async Task AiOutput_DatesTicketNumberAndAmounts_AreNotMistakenForAccountNumbers()
    {
        var body = "{{FULL_NAME}} {{ADDRESS}} W dniu 01.11.2026 (bilet nr IC-4821-0093, 2026-11-01) opóźnienie 70 min. "
                   + "Odszkodowanie 10,00 zł od ceny 40,00 zł, art. 19 rozporządzenia (UE) 2021/782. {{PAYOUT}}";

        var letter = await GenerateAsync(new FakeAi(ValidAiJson(body)), Req());

        Assert.True(letter.UsedAi);
    }

    [Theory]
    [InlineData(Verdict.NotCovered, 0, 10)]
    [InlineData(Verdict.Unknown, 0, 10)]
    [InlineData(Verdict.Eligible, 0, 0)]
    public async Task NotEligibleCase_ReturnsNotEligibleAndWritesNothing(Verdict verdict, int unused, int amount)
    {
        _ = unused;
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.Verdict = verdict;
        c.CompensationAmount = verdict == Verdict.Eligible ? amount : 0m;
        await _cases.UpdateAsync(c);
        var ai = new FakeAi(ValidAiJson());

        var result = await Service(ai).GenerateAsync(_annaCaseId, Req(PayoutPreference.Voucher, "PL27114020040000300201355387", true), CancellationToken.None);

        Assert.Equal(ComplaintStatus.NotEligible, result.Status);
        Assert.Null(result.Letter);
        Assert.Null(ai.Prompt);
        Assert.Equal(PayoutPreference.OriginalPayment, (await _cases.GetAsync(_annaCaseId, Anna))!.PayoutPreference);
        var profile = await _store.GetAsync(Anna);
        Assert.Equal(PayoutPreference.OriginalPayment, profile!.PreferredPayout);
        Assert.Equal(Iban, profile.Iban);
    }

    [Fact]
    public async Task RenderPdf_RendersPostedTextForOwnerOnly()
    {
        var request = new ComplaintPdfRequest("Temat", "Treść");

        var own = await Service(Failing()).RenderPdfAsync(_annaCaseId, request);
        var foreign = await Service(Failing()).RenderPdfAsync(_otherCaseId, request);

        Assert.Equal([1, 2, 3], own);
        Assert.Equal("Temat", _pdf.Last!.Subject);
        Assert.Equal("Treść", _pdf.Last.Body);
        Assert.Null(foreign);
        Assert.Equal(1, _pdf.Calls);
    }

    [Fact]
    public async Task Generating_WithUnchangedPayoutAndNoIbanToSave_DoesNotWriteProfile()
    {
        await GenerateAsync(Failing(), Req(PayoutPreference.OriginalPayment, "PL27114020040000300201355387"));

        Assert.Empty(_profileWrites);
    }

    [Fact]
    public async Task Generating_WithChangedPayout_WritesOnlyThePayoutChange()
    {
        await GenerateAsync(Failing(), Req(PayoutPreference.Voucher));

        var written = Assert.Single(_profileWrites);
        Assert.Equal(new UserProfile("Anna Kowalska", "ul. Lipowa 12/4, 80-001 Gdańsk", Iban, PayoutPreference.Voucher), written);
    }

    [Fact]
    public async Task Template_MissingStations_UsesPlaceholdersAndReportsThem()
    {
        var id = await AddCaseAsync(Anna, noStations: true);

        var letter = await GenerateAsync(Failing(), Req(), caseId: id);

        Assert.Contains("[stacja początkowa]", letter.Body);
        Assert.Contains("[stacja docelowa]", letter.Body);
        Assert.DoesNotContain("{{", letter.Body);
        Assert.Equal(["stacja początkowa", "stacja docelowa"], letter.MissingFields);
    }

    [Fact]
    public async Task AiPrompt_SaysStationsAreUnknownInsteadOfInventingThem()
    {
        var id = await AddCaseAsync(Anna, noStations: true);
        var ai = new FakeAi(ValidAiJson());

        await GenerateAsync(ai, Req(), caseId: id);

        Assert.Contains("unknown", ai.Prompt!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{{FROM_STATION}}", ai.Prompt);
        Assert.DoesNotContain("Gdynia", ai.Prompt);
    }

    [Fact]
    public async Task AiOutput_WithStationTokensForMissingStations_IsAcceptedAndFilled()
    {
        var id = await AddCaseAsync(Anna, noStations: true);
        var ai = new FakeAi(ValidAiJson(
            "{{FULL_NAME}}\n{{ADDRESS}}\n\nPrzejazd {{FROM_STATION}} - {{TO_STATION}}: 10,00 zł, art. 19 rozporządzenia (UE) 2021/782.\n\n{{PAYOUT}}\n\n{{FULL_NAME}}"));

        var letter = await GenerateAsync(ai, Req(), caseId: id);

        Assert.True(letter.UsedAi);
        Assert.Contains("Przejazd [stacja początkowa] - [stacja docelowa]", letter.Body);
        Assert.Equal(["stacja początkowa", "stacja docelowa"], letter.MissingFields);
    }

    [Fact]
    public async Task AiOutput_WithStationTokensWhenStationsKnown_FallsBackToTemplate()
    {
        var ai = new FakeAi(ValidAiJson(
            "{{FULL_NAME}}\n{{ADDRESS}}\n\n{{FROM_STATION}}: 10,00 zł, art. 19 rozporządzenia (UE) 2021/782.\n\n{{PAYOUT}}"));

        var letter = await GenerateAsync(ai, Req());

        Assert.False(letter.UsedAi);
        Assert.Contains("Gdynia Główna", letter.Body);
    }
}
