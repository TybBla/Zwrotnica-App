using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Services;
using RefundPilot.Domain;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Application;

public sealed class ReminderServiceTests : IAsyncLifetime
{
    private const string Anna = "user-anna";
    private const string Piotr = "user-piotr";
    private const string OptedOut = "user-optout";
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Records every attempt and message; throws for the addresses in <see cref="FailFor"/> / <see cref="Throws"/>.</summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Text, string Html)> Sent { get; } = [];
        public List<string> Attempts { get; } = [];
        public HashSet<string> FailFor { get; } = [];
        public Dictionary<string, Exception> Throws { get; } = [];

        public Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct)
        {
            Attempts.Add(to);
            if (Throws.TryGetValue(to, out var error))
                throw error;
            if (FailFor.Contains(to))
                throw new InvalidOperationException("SMTP down");
            Sent.Add((to, subject, textBody, htmlBody));
            return Task.CompletedTask;
        }
    }

    /// <summary>Another instance claims every due case between this run's query and its claim.</summary>
    private sealed class RacingRepository(ICaseRepository inner) : ICaseRepository
    {
        public Task<List<RefundCase>> ListAsync(string userId) => inner.ListAsync(userId);
        public Task<RefundCase?> GetAsync(int id, string userId) => inner.GetAsync(id, userId);
        public Task AddAsync(RefundCase refundCase) => inner.AddAsync(refundCase);
        public Task UpdateAsync(RefundCase refundCase) => inner.UpdateAsync(refundCase);
        public Task<bool> DeleteAsync(int id, string userId) => inner.DeleteAsync(id, userId);

        public async Task<List<DueReminder>> ListDueRemindersAsync(DateTime dueBefore, CancellationToken ct)
        {
            var due = await inner.ListDueRemindersAsync(dueBefore, ct);
            await inner.ClaimRemindersAsync(due.Select(d => d.Case.Id).ToList(), Now.AddSeconds(-1), ct);
            return due;
        }

        public Task<List<int>> ClaimRemindersAsync(IReadOnlyCollection<int> caseIds, DateTime claimedAt, CancellationToken ct) =>
            inner.ClaimRemindersAsync(caseIds, claimedAt, ct);

        public Task ReleaseRemindersAsync(IReadOnlyCollection<int> caseIds, DateTime claimedAt, CancellationToken ct) =>
            inner.ReleaseRemindersAsync(caseIds, claimedAt, ct);
    }

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeEmailSender _email = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now));
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _db.Users.AddRange(
            new AppUser { Id = Anna, UserName = "anna@test.pl", Email = "anna@test.pl" },
            new AppUser { Id = Piotr, UserName = "piotr@test.pl", Email = "piotr@test.pl" },
            new AppUser { Id = OptedOut, UserName = "off@test.pl", Email = "off@test.pl", EmailReminders = false });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ReminderService Service(ICaseRepository? repository = null) => new(repository ?? new CaseRepository(_db), _email,
        _clock, new ReminderOptions("http://localhost:5173"), NullLogger<ReminderService>.Instance);

    private Task<int> AddCaseAsync(string userId, CaseStatus status, int dueInDays, DateTime? remindedAt = null) =>
        AddCaseDueAtAsync(userId, status, Now.AddDays(dueInDays), remindedAt);

    private async Task<int> AddCaseDueAtAsync(string userId, CaseStatus status, DateTime due, DateTime? remindedAt = null)
    {
        var c = new RefundCase
        {
            UserId = userId,
            Carrier = "PKP Intercity",
            TrainCategory = "IC",
            TravelDate = new DateOnly(2026, 10, 1),
            TicketPrice = 100m,
            IncidentType = IncidentType.Delay,
            DelayMinutes = 70,
            Verdict = Verdict.Eligible,
            CompensationAmount = 25m,
            Status = status,
            FiledAt = due.AddMonths(-1),
            ResponseDueDate = due,
            ReminderSentAt = remindedAt,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _db.Cases.Add(c);
        await _db.SaveChangesAsync();
        return c.Id;
    }

    private Task<DateTime?> ReminderSentAtAsync(int id) =>
        _db.Cases.AsNoTracking().Where(c => c.Id == id).Select(c => c.ReminderSentAt).SingleAsync();

    [Fact]
    public async Task SendsOnlyForSentCasesPastDue_NotRemindedYet_WhoseOwnerOptedIn()
    {
        var due = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -1);
        var notYetDue = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: 1);
        var paid = await AddCaseAsync(Anna, CaseStatus.Paid, dueInDays: -5);
        var rejected = await AddCaseAsync(Anna, CaseStatus.Rejected, dueInDays: -5);
        var fresh = await AddCaseAsync(Anna, CaseStatus.New, dueInDays: -5);
        var reminded = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -5, remindedAt: Now.AddDays(-3));
        var optedOut = await AddCaseAsync(OptedOut, CaseStatus.Sent, dueInDays: -5);

        var count = await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(1, count);
        var message = Assert.Single(_email.Sent);
        Assert.Equal("anna@test.pl", message.To);
        Assert.Contains($"http://localhost:5173/sprawy/{due}", message.Text);
        Assert.Equal(Now, await ReminderSentAtAsync(due));
        foreach (var id in new[] { notYetDue, paid, rejected, fresh, optedOut })
            Assert.Null(await ReminderSentAtAsync(id));
        Assert.Equal(Now.AddDays(-3), await ReminderSentAtAsync(reminded));
    }

    [Fact]
    public async Task EachCaseIsRemindedOnce()
    {
        await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -1);

        await Service().SendDueAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(1));
        var second = await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task OneFailedSend_DoesNotStopOthers_AndIsRetriedOnTheNextRun()
    {
        var failing = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -2);
        var ok = await AddCaseAsync(Piotr, CaseStatus.Sent, dueInDays: -2);
        _email.FailFor.Add("anna@test.pl");

        var first = await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal("piotr@test.pl", Assert.Single(_email.Sent).To);
        Assert.Null(await ReminderSentAtAsync(failing));
        Assert.Equal(Now, await ReminderSentAtAsync(ok));

        _email.FailFor.Clear();
        var retry = await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(1, retry);
        Assert.Equal("anna@test.pl", _email.Sent[^1].To);
        Assert.NotNull(await ReminderSentAtAsync(failing));
    }

    [Fact]
    public async Task EachOwnerGetsOnlyTheirOwnCase()
    {
        var annas = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -1);
        var piotrs = await AddCaseAsync(Piotr, CaseStatus.Sent, dueInDays: -1);

        await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        var toAnna = _email.Sent.Single(m => m.To == "anna@test.pl");
        Assert.Contains($"/sprawy/{annas}", toAnna.Text);
        Assert.DoesNotContain($"/sprawy/{piotrs}", toAnna.Text);
    }

    /// <summary>The carrier has the whole last day (art. 112 KC): remind only from the next Warsaw calendar day.</summary>
    [Fact]
    public async Task DeadlineDay_IsLeftToTheCarrier_ReminderStartsTheNextWarsawDay()
    {
        // Now is 13.11 11:00 in Warsaw (UTC+1).
        var endedYesterday = await AddCaseDueAtAsync(Anna, CaseStatus.Sent, new DateTime(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc));
        var endsTodayJustAfterMidnight = await AddCaseDueAtAsync(Anna, CaseStatus.Sent, new DateTime(2026, 11, 12, 23, 30, 0, DateTimeKind.Utc));
        var endsTodayEarlier = await AddCaseDueAtAsync(Anna, CaseStatus.Sent, new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc));

        await Service().SendDueAsync(CancellationToken.None);

        Assert.NotNull(await ReminderSentAtAsync(endedYesterday));
        Assert.Null(await ReminderSentAtAsync(endsTodayJustAfterMidnight));
        Assert.Null(await ReminderSentAtAsync(endsTodayEarlier));
        Assert.Single(_email.Sent);
    }

    [Theory]
    [InlineData(EmailRejection.InvalidAddress)]
    [InlineData(EmailRejection.RecipientRefused)]
    [InlineData(EmailRejection.ReservedDomain)]
    public async Task PermanentRejection_IsMarkedHandled_NotRetried_AndOthersStillGo(EmailRejection reason)
    {
        var rejected = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -2);
        var ok = await AddCaseAsync(Piotr, CaseStatus.Sent, dueInDays: -2);
        _email.Throws["anna@test.pl"] = new EmailRecipientRejectedException(reason);

        var sent = await Service().SendDueAsync(CancellationToken.None);
        await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(1, sent);
        Assert.NotNull(await ReminderSentAtAsync(rejected));
        Assert.NotNull(await ReminderSentAtAsync(ok));
        Assert.Single(_email.Attempts, "anna@test.pl");
    }

    [Fact]
    public async Task TransportFailure_StopsTheRun_AndReleasesEveryUnsentCase()
    {
        var first = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -2);
        var second = await AddCaseAsync(Piotr, CaseStatus.Sent, dueInDays: -2);
        _email.Throws["anna@test.pl"] = new EmailTransportException("connect failed");

        var sent = await Service().SendDueAsync(CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Equal(["anna@test.pl"], _email.Attempts);
        Assert.Null(await ReminderSentAtAsync(first));
        Assert.Null(await ReminderSentAtAsync(second));
    }

    [Fact]
    public async Task CaseClaimedByAnotherRun_IsNotSentAgain()
    {
        var id = await AddCaseAsync(Anna, CaseStatus.Sent, dueInDays: -2);

        var sent = await Service(new RacingRepository(new CaseRepository(_db))).SendDueAsync(CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Empty(_email.Attempts);
        Assert.Equal(Now.AddSeconds(-1), await ReminderSentAtAsync(id));
    }
}
