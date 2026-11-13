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
    public async Task Update_PersistsChanges()
    {
        var created = NewCase();
        await _repository.AddAsync(created);

        created.Status = CaseStatus.Sent;
        await _repository.UpdateAsync(created);

        var stored = await _repository.GetAsync(created.Id);
        Assert.Equal(CaseStatus.Sent, stored!.Status);
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
