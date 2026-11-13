using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Application;

public sealed class CaseRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private ICaseRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _repository = new CaseRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static RefundCase NewCase() => new()
    {
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
        PayoutPreference = PayoutPreference.Cash,
        Status = CaseStatus.New,
        CreatedAt = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task AddedCase_IsReturnedByList_WithGeneratedId()
    {
        await _repository.AddAsync(NewCase());

        var cases = await _repository.ListAsync();

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

        Assert.True(await _repository.DeleteAsync(created.Id));
        Assert.False(await _repository.DeleteAsync(created.Id));
        Assert.Null(await _repository.GetAsync(created.Id));
    }

    [Fact]
    public async Task Update_WithDetachedCopy_PersistsChangedFieldsOnly()
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
        var stored = await new CaseRepository(freshDb).GetAsync(created.Id);

        Assert.NotNull(stored);
        Assert.Equal(CaseStatus.Sent, stored.Status);
        Assert.Equal(detached.FiledAt, stored.FiledAt);
        Assert.Equal(detached.ResponseDueDate, stored.ResponseDueDate);
        Assert.Equal("Wysłano formularz", stored.Notes);
        Assert.Equal("PKP Intercity", stored.Carrier);
    }

    [Fact]
    public async Task Seeding_Twice_YieldsExactlyFiveCases()
    {
        await SeedData.EnsureSeededAsync(_db);
        await SeedData.EnsureSeededAsync(_db);

        var cases = await _repository.ListAsync();

        Assert.Equal(5, cases.Count);
        Assert.Contains(cases, c => c.Status == CaseStatus.Paid);
        Assert.Contains(cases, c => c.Verdict == Verdict.NotCovered && c.CompensationAmount == 0m);
    }
}
