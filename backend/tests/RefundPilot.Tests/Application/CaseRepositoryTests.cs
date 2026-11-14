using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Application;

public sealed class CaseRepositoryTests : IAsyncLifetime
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private ICaseRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _repository = new CaseRepository(_db);

        _db.Users.AddRange(
            new AppUser { Id = UserA, UserName = "a@test.pl", Email = "a@test.pl" },
            new AppUser { Id = UserB, UserName = "b@test.pl", Email = "b@test.pl" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static RefundCase NewCase(string userId = UserA) => new()
    {
        UserId = userId,
        Carrier = "PKP Intercity",
        TrainCategory = "IC",
        FromStation = "Gdynia Główna",
        ToStation = "Kraków Główny",
        TravelDate = new DateOnly(2026, 11, 1),
        TicketPrice = 100m,
        IncidentType = IncidentType.Delay,
        DelayMinutes = 95,
        Verdict = Verdict.Eligible,
        CompensationAmount = 25m,
        PayoutPreference = PayoutPreference.BankTransfer,
        Status = CaseStatus.New,
        CreatedAt = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task AddedCase_IsReturnedByList_WithGeneratedId()
    {
        await _repository.AddAsync(NewCase());

        var cases = await _repository.ListAsync(UserA);

        var stored = Assert.Single(cases);
        Assert.True(stored.Id > 0);
        Assert.Equal(CaseStatus.New, stored.Status);
        Assert.Equal(25m, stored.CompensationAmount);
    }

    [Fact]
    public async Task Delete_ReturnsTrueOnce_ThenFalse()
    {
        var created = NewCase();
        await _repository.AddAsync(created);

        Assert.True(await _repository.DeleteAsync(created.Id, UserA));
        Assert.False(await _repository.DeleteAsync(created.Id, UserA));
        Assert.Null(await _repository.GetAsync(created.Id, UserA));
    }

    [Fact]
    public async Task Update_WithDetachedCopy_PersistsWholeEntity()
    {
        var created = NewCase();
        await _repository.AddAsync(created);
        _db.ChangeTracker.Clear();

        var detached = NewCase();
        detached.Id = created.Id;
        detached.Status = CaseStatus.Sent;
        detached.FiledAt = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
        detached.ResponseDueDate = new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc);
        detached.Notes = "Wysłano formularz";
        await _repository.UpdateAsync(detached);

        await using var freshDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        var stored = await new CaseRepository(freshDb).GetAsync(created.Id, UserA);

        Assert.NotNull(stored);
        Assert.Equal(CaseStatus.Sent, stored.Status);
        Assert.Equal(detached.FiledAt, stored.FiledAt);
        Assert.Equal(detached.ResponseDueDate, stored.ResponseDueDate);
        Assert.Equal(DateTimeKind.Utc, stored.FiledAt!.Value.Kind);
        Assert.Equal("Wysłano formularz", stored.Notes);
        Assert.Equal("PKP Intercity", stored.Carrier);
    }

    [Fact]
    public async Task Seeding_Twice_YieldsExactlySixCases()
    {
        await SeedData.EnsureSeededAsync(_db, UserA);
        await SeedData.EnsureSeededAsync(_db, UserA);

        var cases = await _repository.ListAsync(UserA);

        Assert.Equal(6, cases.Count);
        Assert.Contains(cases, c => c.Status == CaseStatus.Paid);
        Assert.Contains(cases, c => c.Verdict == Verdict.NotCovered && c.CompensationAmount == 0m);
        Assert.Null(Assert.Single(cases, c => c.Carrier == "Polregio").TrainCategory);
    }

    [Fact]
    public async Task List_ExcludesOtherUsersCases()
    {
        await _repository.AddAsync(NewCase(UserA));
        await _repository.AddAsync(NewCase(UserB));

        var cases = await _repository.ListAsync(UserA);

        var only = Assert.Single(cases);
        Assert.Equal(UserA, only.UserId);
    }

    [Fact]
    public async Task Get_WithWrongUser_ReturnsNull()
    {
        var created = NewCase(UserA);
        await _repository.AddAsync(created);

        Assert.Null(await _repository.GetAsync(created.Id, UserB));
        Assert.NotNull(await _repository.GetAsync(created.Id, UserA));
    }

    [Theory]
    [InlineData(nameof(RefundCase.Carrier), 200)]
    [InlineData(nameof(RefundCase.TrainCategory), 50)]
    [InlineData(nameof(RefundCase.FromStation), 200)]
    [InlineData(nameof(RefundCase.ToStation), 200)]
    [InlineData(nameof(RefundCase.TicketNumber), 100)]
    public void ColumnLengths_MatchRequestDtoLimits(string property, int expected) =>
        Assert.Equal(expected, _db.Model.FindEntityType(typeof(RefundCase))!.FindProperty(property)!.GetMaxLength());

    [Fact]
    public async Task Delete_WithWrongUser_ReturnsFalse_AndKeepsCase()
    {
        var created = NewCase(UserA);
        await _repository.AddAsync(created);

        Assert.False(await _repository.DeleteAsync(created.Id, UserB));
        Assert.NotNull(await _repository.GetAsync(created.Id, UserA));
    }
}
