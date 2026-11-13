using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Tests.Api;

public sealed class ExtractionsApiTests : IClassFixture<TestApiFactory>
{
    private const string FakeJson = """
        {"carrier":"PKP Intercity","trainCategory":"IC","fromStation":"A","toStation":"B",
         "ticketNumber":"123","ticketPrice":40,"travelDate":"2026-11-01"}
        """;

    private sealed class FakeAi(bool configured, Exception? fail = null) : IAiClient
    {
        public bool IsConfigured => configured;

        public Task<string> GenerateJsonAsync(string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct) =>
            fail is null ? Task.FromResult(FakeJson) : throw fail;

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct) => Task.FromResult("");
    }

    private readonly TestApiFactory _factory;

    public ExtractionsApiTests(TestApiFactory factory) => _factory = factory;

    private HttpClient ClientWith(IAiClient ai) => _factory
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(ai)))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static MultipartFormDataContent Upload(params (string Type, string Name, int Size)[] files)
    {
        var content = new MultipartFormDataContent();
        foreach (var (type, name, size) in files)
        {
            var part = new ByteArrayContent(new byte[size]);
            part.Headers.ContentType = new MediaTypeHeaderValue(type);
            content.Add(part, "files", name);
        }
        return content;
    }

    [Fact]
    public async Task Pdf_IsAcceptedAnonymously()
    {
        var response = await ClientWith(new FakeAi(true)).PostAsync("/api/extractions",
            Upload(("application/pdf", "ticket.pdf", 1024)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("PKP Intercity", json.GetProperty("carrier").GetString());
        Assert.True(json.GetProperty("usedAi").GetBoolean());
        Assert.Equal(0, json.GetProperty("missingFields").GetArrayLength());
    }

    [Fact]
    public async Task TextFile_Returns400()
    {
        var response = await ClientWith(new FakeAi(true)).PostAsync("/api/extractions",
            Upload(("text/plain", "ticket.txt", 10)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SixMegabyteFile_Returns400NotRaw413()
    {
        var response = await ClientWith(new FakeAi(true)).PostAsync("/api/extractions",
            Upload(("image/jpeg", "big.jpg", 6 * 1024 * 1024)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("5 MB", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FiveFiles_Returns400()
    {
        var files = Enumerable.Range(0, 5).Select(i => ("image/png", $"{i}.png", 100)).ToArray();

        var response = await ClientWith(new FakeAi(true)).PostAsync("/api/extractions", Upload(files));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NoFiles_Returns400()
    {
        var response = await ClientWith(new FakeAi(true)).PostAsync("/api/extractions", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NotConfigured_Returns409WithPolishMessage()
    {
        var response = await ClientWith(new FakeAi(false)).PostAsync("/api/extractions",
            Upload(("application/pdf", "ticket.pdf", 100)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("AI wyłączone", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AiUnavailable_Returns502()
    {
        var ai = new FakeAi(true, new AiUnavailableException("Usługa AI jest chwilowo niedostępna."));

        var response = await ClientWith(ai).PostAsync("/api/extractions",
            Upload(("image/heic", "ticket.heic", 100)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task EleventhRequestWithinAMinute_Returns429WithPolishProblem()
    {
        var client = _factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("RateLimit:ExtractionsPerMinute", "10");
                b.ConfigureTestServices(s => s.AddSingleton<IAiClient>(new FakeAi(true)));
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < 10; i++)
        {
            var ok = await client.PostAsync("/api/extractions", Upload(("application/pdf", "t.pdf", 100)));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        var limited = await client.PostAsync("/api/extractions", Upload(("application/pdf", "t.pdf", 100)));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("Zbyt wiele", await limited.Content.ReadAsStringAsync());
    }
}
