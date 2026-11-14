using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
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
        public string? LastPlaceAndDate { get; private set; }

        public byte[] Render(ComplaintDto letter, string placeAndDate)
        {
            Calls++;
            Last = letter;
            LastPlaceAndDate = placeAndDate;
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

    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now));

    private ComplaintService Service(IAiClient ai, string userId = Anna) =>
        new(_cases, new RecordingStore(_store, _profileWrites), new FakeUser(userId), ai, new EfUnitOfWork(_db), _pdf, _clock);

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

        // The wizard never asks whether the passenger travelled another way, so the letter states neither.
        Assert.Contains(
            "Wniosek dotyczy przejazdu w dniu 01.11.2026 pociągiem kat. IC relacją Gdynia Główna – Kraków Główny na podstawie " +
            "biletu nr IC-4821-0093, za który zapłaciłem(-am) 40,00 zł. Pociąg został odwołany.", letter.Body);
        foreach (var claim in new[] { "odbyłem", "zrezygnow", "0 min" })
            Assert.DoesNotContain(claim, letter.Body);
    }

    [Fact]
    public async Task Template_Delay_StillSaysThePassengerTravelled()
    {
        var letter = await GenerateAsync(Failing(), Req());

        Assert.Contains(
            "W dniu 01.11.2026 odbyłem(-am) podróż pociągiem kat. IC relacją Gdynia Główna – Kraków Główny na podstawie " +
            "biletu nr IC-4821-0093, za który zapłaciłem(-am) 40,00 zł. Pociąg dotarł do stacji docelowej z opóźnieniem 70 min.", letter.Body);
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
        Assert.Contains("The train was cancelled. Do not state whether the passenger travelled or gave up the journey.", ai.Prompt);
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

    /// <summary>A covered train with nothing due under the EU rules (e.g. 45 minutes late) still gets a carrier-rules complaint.</summary>
    [Fact]
    public async Task EligibleCaseWithNothingDue_GetsCarrierRulesComplaint()
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.CompensationAmount = 0m;
        c.DelayMinutes = 45;
        await _cases.UpdateAsync(c);

        var result = await Service(Failing()).GenerateAsync(_annaCaseId, Req(PayoutPreference.Voucher), CancellationToken.None);

        Assert.Equal(ComplaintStatus.Generated, result.Status);
        var letter = result.Letter!;
        Assert.Equal("Reklamacja – opóźnienie pociągu PKP Intercity z dnia 01.11.2026", letter.Subject);
        Assert.Contains("45 min", letter.Body);
        Assert.Contains("regulamin", letter.Body);
        Assert.Contains("W przypadku przyznania rekompensaty proszę o jej wypłatę w formie bonu.", letter.Body);
        foreach (var banned in new[] { "art. 19", "art. 18", "2021/782", " 0,00 zł" })
            Assert.DoesNotContain(banned, letter.Body);
        Assert.Equal(PayoutPreference.Voucher, (await _cases.GetAsync(_annaCaseId, Anna))!.PayoutPreference);
    }

    [Fact]
    public async Task EligibleCaseWithNothingDue_AiDraftIsValidatedAsCarrierRules()
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.CompensationAmount = 0m;
        c.DelayMinutes = 45;
        await _cases.UpdateAsync(c);
        var ai = new FakeAi(ValidCarrierRulesJson());

        var letter = await GenerateAsync(ai, Req());

        Assert.True(letter.UsedAi);
        Assert.Contains("regulamin", ai.Prompt);
        Assert.DoesNotContain("Amount requested", ai.Prompt);
    }

    /// <summary>Without a ticket price there is nothing to describe: the only input the letter refuses (409).</summary>
    [Fact]
    public async Task CaseWithoutTicketPrice_ReturnsPriceRequiredAndWritesNothing()
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.CompensationAmount = 0m;
        c.TicketPrice = 0m;
        await _cases.UpdateAsync(c);
        var ai = new FakeAi(ValidAiJson());

        var result = await Service(ai).GenerateAsync(_annaCaseId, Req(PayoutPreference.Voucher, "PL27114020040000300201355387", true), CancellationToken.None);

        Assert.Equal(ComplaintStatus.PriceRequired, result.Status);
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
        Assert.Equal("Gdańsk, 13 listopada 2026 r.", _pdf.LastPlaceAndDate);
        Assert.Null(foreign);
        Assert.Equal(1, _pdf.Calls);
    }

    [Fact]
    public async Task RenderPdf_WithoutProfileAddress_UsesTheDestinationCity()
    {
        await Service(Failing(), Empty).RenderPdfAsync(await AddCaseAsync(Empty), new ComplaintPdfRequest("Temat", "Treść"));

        Assert.Equal("Kraków, 13 listopada 2026 r.", _pdf.LastPlaceAndDate);
    }

    [Fact]
    public async Task RenderPdf_WithoutAddressOrStations_UsesPlaceholder()
    {
        await Service(Failing(), Empty).RenderPdfAsync(await AddCaseAsync(Empty, noStations: true), new ComplaintPdfRequest("Temat", "Treść"));

        Assert.Equal("[miejscowość], 13 listopada 2026 r.", _pdf.LastPlaceAndDate);
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

    // ---------- Carrier-rules complaint ("reklamacja") for trains outside the EU 25%/50% scheme ----------

    /// <summary>Turns Anna's case into one with no EU amount: a regional carrier (NotCovered) or an unclear one (Unknown).</summary>
    private async Task MakeAnnaCaseUncoveredAsync(Verdict verdict, IncidentType incident = IncidentType.Delay)
    {
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.Carrier = "Polregio";
        c.TrainCategory = null;
        c.Verdict = verdict;
        c.CompensationAmount = 0m;
        c.IncidentType = incident;
        c.DelayMinutes = incident == IncidentType.Cancellation ? 0 : 70;
        await _cases.UpdateAsync(c);
    }

    private static string ValidCarrierRulesJson(
        string body = "{{FULL_NAME}}\n{{ADDRESS}}\n\nSkładam reklamację: opóźnienie 70 min, bilet za 40,00 zł. Proszę o rozpatrzenie według regulaminu przewozu.\n\n{{PAYOUT}}\n\nZ poważaniem\n{{FULL_NAME}}") =>
        JsonSerializer.Serialize(new { subject = "Reklamacja – opóźnienie pociągu Polregio", body });

    [Theory]
    [InlineData(Verdict.NotCovered)]
    [InlineData(Verdict.Unknown)]
    public async Task UncoveredCase_Template_IsAComplaintUnderTheCarriersRules(Verdict verdict)
    {
        await MakeAnnaCaseUncoveredAsync(verdict);

        var result = await Service(Failing()).GenerateAsync(_annaCaseId, Req(PayoutPreference.Voucher), CancellationToken.None);

        Assert.Equal(ComplaintStatus.Generated, result.Status);
        var letter = result.Letter!;
        Assert.False(letter.UsedAi);
        Assert.Equal("Reklamacja – opóźnienie pociągu Polregio z dnia 01.11.2026", letter.Subject);
        foreach (var expected in new[]
                 { "Anna Kowalska", "ul. Lipowa 12/4, 80-001 Gdańsk", "Polregio", "Gdynia Główna", "Kraków Główny",
                   "IC-4821-0093", "70 min", "40,00 zł", "regulamin", "pisemn" })
            Assert.Contains(expected, letter.Body);
        Assert.Contains("W przypadku przyznania rekompensaty proszę o jej wypłatę w formie bonu.", letter.Body);
        foreach (var banned in new[] { "art. 19", "art. 18", "2021/782", "Wnoszę o", " 0,00 zł", "{{" })
            Assert.DoesNotContain(banned, letter.Body);
        Assert.Empty(letter.MissingFields);
        Assert.Equal(PayoutPreference.Voucher, (await _cases.GetAsync(_annaCaseId, Anna))!.PayoutPreference);
    }

    [Fact]
    public async Task UncoveredCancellation_Template_DescribesTheCancellation()
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered, IncidentType.Cancellation);

        var letter = await GenerateAsync(Failing(), Req());

        Assert.Equal("Reklamacja – odwołanie pociągu Polregio z dnia 01.11.2026", letter.Subject);
        Assert.Contains(
            "składam reklamację dotyczącą przejazdu w dniu 01.11.2026 pociągiem relacją Gdynia Główna – Kraków Główny na podstawie " +
            "biletu nr IC-4821-0093, za który zapłaciłem(-am) 40,00 zł. Pociąg został odwołany.", letter.Body);
        Assert.DoesNotContain("odbytego", letter.Body);
        Assert.Contains("W przypadku przyznania zwrotu należności lub rekompensaty proszę o wypłatę na rachunek bankowy lub kartę, z których opłacono bilet.", letter.Body);
        Assert.DoesNotContain("0 min", letter.Body);
    }

    [Fact]
    public async Task UncoveredCancellation_AiPrompt_DoesNotAssumeWhatThePassengerDid()
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered, IncidentType.Cancellation);
        var ai = new FakeAi(ValidCarrierRulesJson());

        await GenerateAsync(ai, Req());

        Assert.Contains("- Incident: The train was cancelled. Do not state whether the passenger travelled or gave up the journey.", ai.Prompt);
    }

    [Fact]
    public async Task UncoveredCase_BankTransferWithoutIban_ReportsAccountNumberAsMissing()
    {
        var id = await AddCaseAsync(Empty);
        var c = await _cases.GetAsync(id, Empty);
        c!.Verdict = Verdict.NotCovered;
        c.CompensationAmount = 0m;
        await _cases.UpdateAsync(c);

        var letter = await GenerateAsync(Failing(), Req(PayoutPreference.BankTransfer), Empty, id);

        Assert.Contains("W przypadku przyznania rekompensaty proszę o jej wypłatę przelewem na rachunek nr [numer rachunku].", letter.Body);
        Assert.Equal(["imię i nazwisko", "adres", "numer rachunku"], letter.MissingFields);
    }

    [Theory]
    [InlineData(Verdict.NotCovered)]
    [InlineData(Verdict.Unknown)]
    public async Task UncoveredCase_ValidAiOutput_IsUsedWithTheServersConditionalPayout(Verdict verdict)
    {
        await MakeAnnaCaseUncoveredAsync(verdict);
        var ai = new FakeAi(ValidCarrierRulesJson());

        var letter = await GenerateAsync(ai, Req(PayoutPreference.Voucher));

        Assert.True(letter.UsedAi);
        Assert.Equal("Reklamacja – opóźnienie pociągu Polregio", letter.Subject);
        Assert.Contains("W przypadku przyznania rekompensaty proszę o jej wypłatę w formie bonu.", letter.Body);
        Assert.Contains("Anna Kowalska", letter.Body);
        Assert.DoesNotContain("{{", letter.Body);
    }

    [Fact]
    public async Task UncoveredCase_AiPrompt_AsksForCarrierRulesWithoutAmountOrPersonalData()
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered);
        var ai = new FakeAi(ValidCarrierRulesJson());

        await GenerateAsync(ai, Req(PayoutPreference.BankTransfer, "PL27114020040000300201355387"));

        Assert.Contains("Polregio", ai.Prompt);
        Assert.Contains("40,00 zł", ai.Prompt);
        Assert.Contains("regulamin", ai.Prompt);
        Assert.Contains("{{FULL_NAME}}", ai.Prompt);
        Assert.DoesNotContain(" 0,00 zł", ai.Prompt);
        Assert.DoesNotContain("Amount requested", ai.Prompt);
        foreach (var secret in new[] { "Anna", "Kowalska", "Lipowa", Iban, "PL27" })
            Assert.DoesNotContain(secret, ai.Prompt);
    }

    [Theory]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, bilet 40,00 zł, art. 19 rozporządzenia (UE) 2021/782 {{PAYOUT}}")] // EU entitlement
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, zwrot na podstawie art. 18 {{PAYOUT}}")]                          // EU refund article
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, wnoszę o 10,00 zł {{PAYOUT}}")]                                   // demanded amount
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, bilet 40,00 zł i 20 zł {{PAYOUT}}")]                              // any other sum
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin {{PAYOUT}} Konto PL61 1090 1014 0000 0712 1981 2874")]               // IBAN-like
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin {{PAYOUT}} wyłącznie przelewem")]                                   // exclusive clause
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin {{PAYOUT}} nie przyjmę gotówki")]                                   // cash wording
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, bilet 40,00 zł")]                                                  // no payout token
    [InlineData("{{ADDRESS}} regulamin {{PAYOUT}}")]                                                                     // no name token
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin {{PAYOUT}} {{IBAN}}")]                                              // IBAN token, not a transfer
    [InlineData("{{FULL_NAME}} {{ADDRESS}} {{PAYOUT}} proszę o rozpatrzenie")]                                           // no carrier rules
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, bilet 40,00 zł, zgodnie z 2021/782 {{PAYOUT}}")]                   // EU act by number
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin, bilet 40,00 zł, zgodnie z rozporządzeniem UE {{PAYOUT}}")]         // EU regulation, no article
    [InlineData("{{FULL_NAME}} {{ADDRESS}} regulamin i przepisy Rozporządzenia o prawach pasażerów {{PAYOUT}}")]         // capitalised
    public async Task UncoveredCase_InvalidAiOutput_FallsBackToTemplate(string body)
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered);

        var letter = await GenerateAsync(new FakeAi(ValidCarrierRulesJson(body)), Req());

        Assert.False(letter.UsedAi);
        Assert.StartsWith("Reklamacja – opóźnienie pociągu Polregio", letter.Subject);
        Assert.Contains("regulamin", letter.Body);
        Assert.DoesNotContain("art. 19", letter.Body);
    }

    [Fact]
    public async Task EligibleCase_AiOutputWithoutAmountOrArticle_StillFallsBackToCompensationTemplate()
    {
        var letter = await GenerateAsync(new FakeAi(ValidCarrierRulesJson()), Req());

        Assert.False(letter.UsedAi);
        Assert.StartsWith("Wniosek o wypłatę odszkodowania", letter.Subject);
        Assert.Contains("art. 19", letter.Body);
    }

    // ---------- Regional carrier with documented costs: a claim for reimbursement of proven damage ----------

    private const string Taxi = "Taksówka z Gdyni do Krakowa po opóźnieniu pociągu";
    private const string Attachments = "Do wniosku załączam: [kopie rachunków / paragonów potwierdzających poniesione koszty]";

    private async Task MakeAnnaCaseRegionalWithCostsAsync(
        IncidentType incident = IncidentType.Delay, string? description = Taxi, decimal? amount = 85m)
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered, incident);
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.ExpenseDescription = description;
        c.ExpenseAmount = amount;
        await _cases.UpdateAsync(c);
    }

    private const string ValidCostsBody =
        "{{FULL_NAME}}\n{{ADDRESS}}\n\nSzanowni Państwo,\nwnoszę o zwrot udokumentowanych kosztów w kwocie 85,00 zł, poniesionych " +
        "w związku z opóźnieniem pociągu o 70 minut (bilet za 40,00 zł). Poniesione koszty: {{EXPENSE_DESCRIPTION}}.\n\n{{PAYOUT}}\n\n{{ATTACHMENTS}}\n\nZ poważaniem\n{{FULL_NAME}}";

    private static string CostsJson(string body = ValidCostsBody) =>
        JsonSerializer.Serialize(new { subject = "Wniosek o zwrot kosztów – opóźnienie pociągu Polregio", body });

    [Fact]
    public async Task RegionalCaseWithCosts_Template_ClaimsTheDocumentedCosts()
    {
        await MakeAnnaCaseRegionalWithCostsAsync();

        var letter = await GenerateAsync(Failing(), Req(PayoutPreference.Voucher));

        Assert.False(letter.UsedAi);
        Assert.Equal("Wniosek o zwrot kosztów poniesionych w związku z opóźnieniem pociągu – bilet nr IC-4821-0093", letter.Subject);
        foreach (var expected in new[]
                 {
                     "Anna Kowalska", "Polregio", "Gdynia Główna", "Kraków Główny", "40,00 zł",
                     "Pociąg dotarł do stacji docelowej z opóźnieniem 70 min.",
                     $"W związku z opóźnieniem pociągu poniosłem(-am) następujące koszty: {Taxi}.",
                     "Na podstawie przepisów prawa przewozowego oraz Kodeksu cywilnego, a także regulaminu przewozu wnoszę o zwrot " +
                     "udokumentowanych kosztów w kwocie 85,00 zł. Koszty te są bezpośrednim następstwem opóźnienia pociągu.",
                     "Proszę o przekazanie zwrotu kosztów w formie bonu.",
                     Attachments,
                     "Proszę o rozpatrzenie wniosku i pisemną odpowiedź.",
                 })
            Assert.Contains(expected, letter.Body);
        foreach (var banned in new[] { "2021/782", "rozporządzeni", "art.", "25%", "50%", "W przypadku przyznania", "{{" })
            Assert.DoesNotContain(banned, letter.Body);
        Assert.Equal(["załączniki – rachunki"], letter.MissingFields);
    }

    [Fact]
    public async Task RegionalCaseWithCosts_Cancellation_TemplateSpeaksOfTheCancellation()
    {
        await MakeAnnaCaseRegionalWithCostsAsync(IncidentType.Cancellation, "Nocleg w hotelu.\nTaksówka rano");

        var letter = await GenerateAsync(Failing(), Req());

        Assert.Equal("Wniosek o zwrot kosztów poniesionych w związku z odwołaniem pociągu – bilet nr IC-4821-0093", letter.Subject);
        Assert.Contains("Pociąg został odwołany.", letter.Body);
        Assert.Contains("W związku z odwołaniem pociągu poniosłem(-am) następujące koszty: Nocleg w hotelu; Taksówka rano.", letter.Body);
        Assert.Contains("Koszty te są bezpośrednim następstwem odwołania pociągu.", letter.Body);
        Assert.Contains("Proszę o przekazanie zwrotu kosztów na rachunek bankowy lub kartę, z których opłacono bilet.", letter.Body);
    }

    [Fact]
    public async Task RegionalCaseWithCostsButNoDescription_UsesAPlaceholder()
    {
        await MakeAnnaCaseRegionalWithCostsAsync(description: null);

        var template = await GenerateAsync(Failing(), Req());
        var ai = await GenerateAsync(new FakeAi(CostsJson()), Req());

        Assert.Contains("następujące koszty: [opis poniesionych kosztów].", template.Body);
        Assert.True(ai.UsedAi);
        Assert.Contains("Poniesione koszty: [opis poniesionych kosztów].", ai.Body);
        foreach (var letter in new[] { template, ai })
            Assert.Equal(["opis kosztów", "załączniki – rachunki"], letter.MissingFields);
    }

    [Theory]
    [InlineData("...")]
    [InlineData("!?;:")]
    [InlineData(" . \n ; ")]
    public async Task RegionalCaseWithPunctuationOnlyDescription_UsesAPlaceholder(string description)
    {
        await MakeAnnaCaseRegionalWithCostsAsync(description: description);

        var template = await GenerateAsync(Failing(), Req());
        var ai = await GenerateAsync(new FakeAi(CostsJson()), Req());

        Assert.Contains("następujące koszty: [opis poniesionych kosztów].", template.Body);
        foreach (var letter in new[] { template, ai })
            Assert.Equal(["opis kosztów", "załączniki – rachunki"], letter.MissingFields);
    }

    [Theory]
    [InlineData("Taksówka!", "Taksówka")]
    [InlineData("Taksówka?", "Taksówka")]
    [InlineData("Taksówka:", "Taksówka")]
    [InlineData("Taksówka;\nNocleg!", "Taksówka; Nocleg")]
    public void ExpensesText_StripsTrailingPunctuation(string description, string expected) =>
        Assert.Equal(expected, RefundPilot.Application.ComplaintTemplate.ExpensesText(description));

    [Fact]
    public async Task RegionalCaseWithCosts_AiOutputWithTheDescriptionTokenTwice_FallsBackToTheTemplate()
    {
        await MakeAnnaCaseRegionalWithCostsAsync();

        var letter = await GenerateAsync(new FakeAi(CostsJson(ValidCostsBody + " {{EXPENSE_DESCRIPTION}}")), Req());

        Assert.False(letter.UsedAi);
        Assert.Contains($"następujące koszty: {Taxi}.", letter.Body);
    }

    [Fact]
    public async Task RegionalCaseWithoutCosts_ComplaintReservesTheRightToClaimCosts()
    {
        await MakeAnnaCaseRegionalWithCostsAsync(description: "Taksówka", amount: null);

        var template = await GenerateAsync(Failing(), Req());
        var ai = await GenerateAsync(new FakeAi(ValidCarrierRulesJson()), Req());

        foreach (var letter in new[] { template, ai })
        {
            Assert.StartsWith("Reklamacja", letter.Subject);
            Assert.Contains("Jednocześnie zastrzegam sobie prawo do dochodzenia zwrotu udokumentowanych kosztów poniesionych " +
                            "w związku z opóźnieniem pociągu.", letter.Body);
            Assert.DoesNotContain("85,00 zł", letter.Body);
        }
        Assert.True(ai.UsedAi);
    }

    [Fact]
    public async Task UnclearCase_ComplaintHasNoCostReservation()
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.Unknown);

        var letter = await GenerateAsync(Failing(), Req());

        Assert.DoesNotContain("zastrzegam", letter.Body);
    }

    [Fact]
    public async Task RegionalCaseWithCosts_AiPrompt_HasTheExactAmountButNoPersonalDataOrFreeText()
    {
        await MakeAnnaCaseRegionalWithCostsAsync();
        var ai = new FakeAi(CostsJson());

        await GenerateAsync(ai, Req(PayoutPreference.BankTransfer, "PL27114020040000300201355387"));

        foreach (var expected in new[]
                 { "Polregio", "85,00 zł", "40,00 zł", "verbatim", "{{ATTACHMENTS}}", "{{EXPENSE_DESCRIPTION}}", "Polish diacritics" })
            Assert.Contains(expected, ai.Prompt);
        // The passenger's own description is free text (it may hold personal data): only its token goes to the AI.
        foreach (var secret in new[] { "Anna", "Kowalska", "Lipowa", Iban, "PL27", Taxi, "Taksówka", "Gdyni do Krakowa" })
            Assert.DoesNotContain(secret, ai.Prompt);
    }

    [Theory]
    [InlineData(Verdict.Eligible, 85)]
    [InlineData(Verdict.NotCovered, 85)]
    [InlineData(Verdict.NotCovered, null)]
    [InlineData(Verdict.Unknown, null)]
    public async Task NoPrompt_EverContainsTheCasesFreeText(Verdict verdict, int? amount)
    {
        if (verdict != Verdict.Eligible)
            await MakeAnnaCaseRegionalWithCostsAsync(amount: amount);
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.Verdict = verdict;
        c.Notes = "Notatka prywatna PESEL 90010112345";
        c.ExpenseDescription = "Taksówka z Gdyni, opis prywatny";
        await _cases.UpdateAsync(c);
        var ai = new FakeAi(ValidAiJson());

        await GenerateAsync(ai, Req());

        Assert.NotNull(ai.Prompt);
        foreach (var secret in new[] { "Notatka", "PESEL", "90010112345", "opis prywatny", "Taksówka z Gdyni" })
            Assert.DoesNotContain(secret, ai.Prompt);
    }

    [Fact]
    public async Task RegionalCaseWithCosts_ValidAiOutput_IsUsedAndFilledByTheServer()
    {
        await MakeAnnaCaseRegionalWithCostsAsync();

        var letter = await GenerateAsync(new FakeAi(CostsJson()), Req(PayoutPreference.Voucher));

        Assert.True(letter.UsedAi);
        Assert.Equal("Wniosek o zwrot kosztów – opóźnienie pociągu Polregio", letter.Subject);
        Assert.Contains("Proszę o przekazanie zwrotu kosztów w formie bonu.", letter.Body);
        Assert.Contains(Attachments, letter.Body);
        Assert.Contains("Anna Kowalska", letter.Body);
        Assert.Contains($"Poniesione koszty: {Taxi}.", letter.Body);
        Assert.DoesNotContain("{{", letter.Body);
        Assert.Equal(["załączniki – rachunki"], letter.MissingFields);
    }

    [Fact]
    public async Task RegionalCaseWithCosts_AiOutputWithoutTheDescriptionToken_FallsBackToTheTemplate()
    {
        await MakeAnnaCaseRegionalWithCostsAsync();

        var letter = await GenerateAsync(new FakeAi(CostsJson(ValidCostsBody.Replace(" Poniesione koszty: {{EXPENSE_DESCRIPTION}}.", ""))), Req());

        Assert.False(letter.UsedAi);
        Assert.Contains($"następujące koszty: {Taxi}.", letter.Body);
    }

    [Theory]
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów poniesionych przeze mnie {{PAYOUT}} {{ATTACHMENTS}}")]                 // no amount
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł oraz 20,00 zł {{PAYOUT}} {{ATTACHMENTS}}")]                  // other sum
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85 zł {{PAYOUT}} {{ATTACHMENTS}}")]                                   // amount not verbatim
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł, art. 19 {{PAYOUT}} {{ATTACHMENTS}}")]                       // EU article
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł na podstawie art. 471 Kodeksu cywilnego {{PAYOUT}} {{ATTACHMENTS}}")] // any article
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł zgodnie z 2021/782 {{PAYOUT}} {{ATTACHMENTS}}")]             // EU act
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł według rozporządzenia UE {{PAYOUT}} {{ATTACHMENTS}}")]       // EU regulation
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł {{PAYOUT}} {{ATTACHMENTS}} Konto PL61 1090 1014 0000 0712 1981 2874")] // IBAN-like
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł {{PAYOUT}}")]                                                // no attachments token
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł {{ATTACHMENTS}}")]                                           // no payout token
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł {{PAYOUT}} {{ATTACHMENTS}} {{IBAN}}")]                       // IBAN token, not a transfer
    [InlineData("{{FULL_NAME}} {{ADDRESS}} Prosze o zwrot kosztów 85,00 zł {{PAYOUT}} {{ATTACHMENTS}}")]                       // no diacritics
    [InlineData("{{FULL_NAME}} {{ADDRESS}} zwrot kosztów 85,00 zł wyłącznie przelewem {{PAYOUT}} {{ATTACHMENTS}}")]            // exclusive clause
    public async Task RegionalCaseWithCosts_InvalidAiOutput_FallsBackToTheCostsTemplate(string body)
    {
        await MakeAnnaCaseRegionalWithCostsAsync();
        // Each case breaks one rule; the description token itself is present.
        body = body.Replace("{{ADDRESS}}", "{{ADDRESS}} {{EXPENSE_DESCRIPTION}}");

        var letter = await GenerateAsync(new FakeAi(CostsJson(body)), Req());

        Assert.False(letter.UsedAi);
        Assert.StartsWith("Wniosek o zwrot kosztów poniesionych w związku z opóźnieniem pociągu", letter.Subject);
        Assert.Contains("w kwocie 85,00 zł", letter.Body);
    }

    [Fact]
    public async Task RegionalCaseWithCosts_DescriptionIsSubstitutedAfterValidation()
    {
        // Typed without diacritics, with a sum: checks apply to the AI's own text, the passenger's words go in afterwards.
        await MakeAnnaCaseRegionalWithCostsAsync(description: "Taxi do Krakowa 50 zl, prosze o zwrot\nHotel");

        var letter = await GenerateAsync(new FakeAi(CostsJson()), Req());

        Assert.True(letter.UsedAi);
        Assert.Contains("Poniesione koszty: Taxi do Krakowa 50 zl, prosze o zwrot; Hotel.", letter.Body);
    }

    // ---------- Correct Polish: an AI letter without diacritics ("z tytulu", "prosze") falls back to the template ----------

    private const string LongAsciiCompensationBody =
        "{{FULL_NAME}}\n{{ADDRESS}}\n\nSzanowni Panstwo,\nna podstawie art. 19 rozporzadzenia (UE) 2021/782 wnosze o wyplacenie " +
        "odszkodowania w kwocie 10,00 zł za opozniony przejazd pociagiem IC na trasie Gdynia Główna - Kraków Główny " +
        "w dniu 01.11.2026. Opoznienie na stacji docelowej wynioslo 70 minut.\n\n{{PAYOUT}}\n\nZ powazaniem\n{{FULL_NAME}}";

    private const string LongPolishCompensationBody =
        "{{FULL_NAME}}\n{{ADDRESS}}\n\nSzanowni Państwo,\nna podstawie art. 19 rozporządzenia (UE) 2021/782 wnoszę o wypłacenie " +
        "odszkodowania w kwocie 10,00 zł za opóźniony przejazd pociągiem IC na trasie Gdynia Główna - Kraków Główny " +
        "w dniu 01.11.2026. Opóźnienie na stacji docelowej wyniosło 70 minut.\n\n{{PAYOUT}}\n\nZ poważaniem\n{{FULL_NAME}}";

    [Theory]
    [InlineData("Wniosek o odszkodowanie z tytulu opoznienia", null)]                // subject without diacritics
    [InlineData("Wniosek o odszkodowanie", "{{FULL_NAME}}\n{{ADDRESS}}\nOdszkodowanie 10,00 zł, art. 19 rozporządzenia (UE) 2021/782. Prosze o wypłatę.\n{{PAYOUT}}")]
    [InlineData("Wniosek o odszkodowanie", "{{FULL_NAME}}\n{{ADDRESS}}\nZwracam sie o odszkodowanie 10,00 zł, art. 19 rozporządzenia (UE) 2021/782.\n{{PAYOUT}}")]
    [InlineData("Wniosek o odszkodowanie", "{{FULL_NAME}}\n{{ADDRESS}}\nNalezy mi się 10,00 zł, art. 19 rozporządzenia (UE) 2021/782 za opóźnienie pociagu.\n{{PAYOUT}}")]
    [InlineData("Wniosek o odszkodowanie", LongAsciiCompensationBody)]               // long body, no diacritics beyond the facts
    public async Task AiOutput_WithoutPolishDiacritics_FallsBackToTemplate(string subject, string? body)
    {
        var json = JsonSerializer.Serialize(new { subject, body = body ?? JsonDocument.Parse(ValidAiJson()).RootElement.GetProperty("body").GetString() });

        var letter = await GenerateAsync(new FakeAi(json), Req());

        Assert.False(letter.UsedAi);
        Assert.StartsWith("Wniosek o wypłatę odszkodowania", letter.Subject);
    }

    [Fact]
    public async Task AiOutput_LongLetterInCorrectPolish_IsUsed()
    {
        var letter = await GenerateAsync(new FakeAi(ValidAiJson(LongPolishCompensationBody)), Req());

        Assert.True(letter.UsedAi);
    }

    [Theory]
    [InlineData("Reklamacja z tytulu opoznienia pociagu", null)]
    [InlineData("Reklamacja – opóźnienie pociągu", "{{FULL_NAME}}\n{{ADDRESS}}\nBilet 40,00 zł. Prosze o rozpatrzenie według regulaminu przewozu.\n{{PAYOUT}}")]
    [InlineData("Reklamacja – opóźnienie pociągu",
        "{{FULL_NAME}}\n{{ADDRESS}}\n\nSzanowni Panstwo,\nskladam reklamacje dotyczaca przejazdu pociagiem Polregio na trasie Gdynia Główna - " +
        "Kraków Główny w dniu 01.11.2026. Pociag przyjechal z opoznieniem 70 minut, bilet kosztowal 40,00 zł. Wnosze o rozpatrzenie " +
        "reklamacji wedlug regulaminu przewozu i pisemna odpowiedz.\n\n{{PAYOUT}}\n\nZ powazaniem\n{{FULL_NAME}}")]
    public async Task UncoveredCase_AiOutputWithoutPolishDiacritics_FallsBackToTemplate(string subject, string? body)
    {
        await MakeAnnaCaseUncoveredAsync(Verdict.NotCovered);
        var json = JsonSerializer.Serialize(new
        {
            subject,
            body = body ?? JsonDocument.Parse(ValidCarrierRulesJson()).RootElement.GetProperty("body").GetString(),
        });

        var letter = await GenerateAsync(new FakeAi(json), Req());

        Assert.False(letter.UsedAi);
        Assert.Equal("Reklamacja – opóźnienie pociągu Polregio z dnia 01.11.2026", letter.Subject);
    }

    [Theory]
    [InlineData(Verdict.Eligible)]
    [InlineData(Verdict.NotCovered)]
    public async Task AiPrompts_AskForCorrectPolishWithDiacritics(Verdict verdict)
    {
        if (verdict != Verdict.Eligible)
            await MakeAnnaCaseUncoveredAsync(verdict);
        var ai = new FakeAi(ValidAiJson());

        await GenerateAsync(ai, Req());

        Assert.Contains("Polish diacritics", ai.Prompt);
    }

    /// <summary>We assume the letter goes out the day it is generated: the carrier's month starts now.</summary>
    [Fact]
    public async Task FirstLetterForNewCase_MarksItSent_AndStartsTheResponseClock()
    {
        var result = await Service(Failing()).GenerateAsync(_annaCaseId, Req(), CancellationToken.None);

        var stored = await _db.Cases.AsNoTracking().SingleAsync(c => c.Id == _annaCaseId);
        Assert.Equal(CaseStatus.Sent, stored.Status);
        Assert.Equal(Now, stored.FiledAt);
        Assert.Equal(Now.AddMonths(1), stored.ResponseDueDate);
        Assert.Null(stored.ReminderSentAt);
        Assert.True(result.Letter!.DeadlineStarted);
        Assert.Equal(Now.AddMonths(1), result.Letter.ResponseDueDate);
    }

    [Fact]
    public async Task ProfileWriteFromTheLetter_KeepsTheRemindersOptOut()
    {
        var profile = await _store.GetAsync(Anna);
        await _store.UpdateAsync(Anna, profile! with { EmailReminders = false });

        await GenerateAsync(Failing(), Req(PayoutPreference.Voucher));

        var after = await _store.GetAsync(Anna);
        Assert.Equal(PayoutPreference.Voucher, after!.PreferredPayout);
        Assert.False(after.EmailReminders);
    }

    [Theory]
    [InlineData(CaseStatus.Sent)]
    [InlineData(CaseStatus.Paid)]
    [InlineData(CaseStatus.Rejected)]
    public async Task RegeneratingLetter_ForCaseAlreadyPastNew_KeepsStatusAndDates(CaseStatus status)
    {
        var filedAt = Now.AddDays(-10);
        var c = await _cases.GetAsync(_annaCaseId, Anna);
        c!.Status = status;
        c.FiledAt = filedAt;
        c.ResponseDueDate = filedAt.AddMonths(1);
        await _cases.UpdateAsync(c);

        var result = await Service(Failing()).GenerateAsync(_annaCaseId, Req(), CancellationToken.None);

        var stored = await _db.Cases.AsNoTracking().SingleAsync(x => x.Id == _annaCaseId);
        Assert.Equal(status, stored.Status);
        Assert.Equal(filedAt, stored.FiledAt);
        Assert.Equal(filedAt.AddMonths(1), stored.ResponseDueDate);
        Assert.False(result.Letter!.DeadlineStarted);
        Assert.Equal(filedAt.AddMonths(1), result.Letter.ResponseDueDate);
    }
}
