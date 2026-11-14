using Microsoft.Extensions.Time.Testing;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Tests.Application;

public sealed class ExtractionServiceTests
{
    private sealed class FakeAi(string json) : IAiClient
    {
        public bool IsConfigured => true;
        public string? Prompt { get; private set; }
        public IReadOnlyList<AiFile>? Files { get; private set; }

        public Task<string> GenerateJsonAsync(string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct)
        {
            Prompt = prompt;
            Files = files;
            return Task.FromResult(json);
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct) => Task.FromResult(json);
    }

    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero));
    private static readonly IReadOnlyList<AiFile> OneFile = [new AiFile("application/pdf", [1, 2, 3])];

    private static Task<ExtractionDto> RunAsync(string json, FakeAi? ai = null) =>
        new ExtractionService(ai ?? new FakeAi(json), Clock).ExtractAsync(OneFile, CancellationToken.None);

    [Fact]
    public async Task ValidJson_MapsFieldsAndListsUnknownAsMissing()
    {
        var result = await RunAsync("""
            {"carrier":"PKP Intercity","trainCategory":"IC","fromStation":"Warszawa Centralna",
             "toStation":null,"ticketNumber":null,"ticketPrice":59.9,"travelDate":"2026-11-01"}
            """);

        Assert.Equal("PKP Intercity", result.Carrier);
        Assert.Equal("IC", result.TrainCategory);
        Assert.Equal(59.9m, result.TicketPrice);
        Assert.Equal(new DateOnly(2026, 11, 1), result.TravelDate);
        Assert.True(result.UsedAi);
        Assert.Equal(["toStation", "ticketNumber"], result.MissingFields);
    }

    [Fact]
    public async Task MalformedJson_ThrowsAiUnavailable() =>
        await Assert.ThrowsAsync<AiUnavailableException>(() => RunAsync("not json {"));

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2000.01")]
    public async Task ImplausiblePrice_BecomesNullAndMissing(string price)
    {
        var result = await RunAsync($$"""{"carrier":"X","ticketPrice":{{price}},"travelDate":"2026-11-01"}""");

        Assert.Null(result.TicketPrice);
        Assert.Contains("ticketPrice", result.MissingFields);
    }

    [Theory]
    [InlineData("2026-11-14")] // future
    [InlineData("2024-11-12")] // older than two years
    [InlineData("garbage")]
    public async Task ImplausibleDate_BecomesNullAndMissing(string date)
    {
        var result = await RunAsync($$"""{"carrier":"X","ticketPrice":50,"travelDate":"{{date}}"}""");

        Assert.Null(result.TravelDate);
        Assert.Contains("travelDate", result.MissingFields);
    }

    [Fact]
    public async Task SendsFilesAndPromptAskingNotToGuess()
    {
        var ai = new FakeAi("{}");

        var result = await RunAsync("{}", ai);

        Assert.Same(OneFile, ai.Files);
        Assert.Contains("never guess", ai.Prompt);
        Assert.Equal(7, result.MissingFields.Count);
    }

    [Fact]
    public async Task HugeNumber_DoesNotThrowAndBecomesMissing()
    {
        var result = await RunAsync("""{"carrier":"X","ticketPrice":1e400,"travelDate":"2026-11-01"}""");

        Assert.Null(result.TicketPrice);
        Assert.Contains("ticketPrice", result.MissingFields);
    }

    [Fact]
    public async Task LongStrings_AreCappedAtTheCaseRequestLimits()
    {
        var result = await RunAsync($$"""
            {"carrier":"{{new string('a', 500)}}","trainCategory":"{{new string('c', 80)}}",
             "fromStation":"{{new string('f', 500)}}","toStation":"{{new string('t', 150)}}",
             "ticketNumber":"{{new string('9', 500)}}","travelDate":"2026-11-01"}
            """);

        Assert.Equal(200, result.Carrier!.Length);
        Assert.Equal(50, result.TrainCategory!.Length);
        Assert.Equal(200, result.FromStation!.Length);
        Assert.Equal(150, result.ToStation!.Length);
        Assert.Equal(100, result.TicketNumber!.Length);
    }

    [Fact]
    public async Task TodayIsWarsawLocalDate()
    {
        // 23:30 UTC on 12 Nov is already 13 Nov in Warsaw.
        var late = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero));
        var ai = new FakeAi("""{"carrier":"X","ticketPrice":50,"travelDate":"2026-11-13"}""");

        var result = await new ExtractionService(ai, late).ExtractAsync(OneFile, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 13), result.TravelDate);
    }

    [Fact]
    public async Task Extraction_StripsDisallowedCharacters()
    {
        var result = await RunAsync("""
            {"carrier":"PKP Intercity 😀","trainCategory":"IC★","fromStation":"Kraków <Główny>","toStation":"😀",
             "ticketNumber":"IC-1/2026{{X}}","ticketPrice":40,"travelDate":"2026-11-01"}
            """);

        Assert.Equal("PKP Intercity", result.Carrier);
        Assert.Equal("IC", result.TrainCategory);
        Assert.Equal("Kraków Główny", result.FromStation);
        Assert.Null(result.ToStation);
        Assert.Equal("IC-1/2026X", result.TicketNumber);
        Assert.Contains("toStation", result.MissingFields);
    }

    [Fact]
    public async Task Extraction_CanonicalizesSmartPunctuation()
    {
        var result = await RunAsync("""
            {"carrier":"O’Neil  Rail","trainCategory":null,"fromStation":"Gdynia – Gdańsk","toStation":null,
             "ticketNumber":null,"ticketPrice":null,"travelDate":null}
            """);

        Assert.Equal("O'Neil Rail", result.Carrier);
        Assert.Equal("Gdynia - Gdańsk", result.FromStation);
    }

    [Theory]
    [InlineData("PKP INTERCITY", "PKP Intercity")]
    [InlineData("pkp ic", "PKP Intercity")]
    [InlineData("Intercity", "Intercity")]
    [InlineData("ÖBB Intercity", "ÖBB Intercity")]
    [InlineData("Polregio Intercity", "Polregio")]
    [InlineData("koleje slaskie", "Koleje Śląskie")]
    [InlineData("Nieznany Przewoźnik", "Nieznany Przewoźnik")]
    public async Task Carrier_IsMappedToTheDictionaryNameOtherwiseKeptSanitized(string raw, string expected)
    {
        var result = await RunAsync($$"""
            {"carrier":"{{raw}}","trainCategory":"ic","fromStation":null,"toStation":null,"ticketNumber":null,
             "ticketPrice":null,"travelDate":null}
            """);

        Assert.Equal(expected, result.Carrier);
        Assert.Equal("IC", result.TrainCategory);
    }

    [Fact]
    public async Task UnknownCategory_IsKeptAsSanitizedText()
    {
        var result = await RunAsync("""
            {"carrier":"Polregio","trainCategory":"Regio Express","fromStation":null,"toStation":null,
             "ticketNumber":null,"ticketPrice":null,"travelDate":null}
            """);

        Assert.Equal("Polregio", result.Carrier);
        Assert.Equal("Regio Express", result.TrainCategory);
    }
}
