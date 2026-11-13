using System.Net;
using System.Text;
using System.Text.Json;
using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure.Ai;

namespace RefundPilot.Tests.Application;

public sealed class GeminiClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static GeminiClient Client(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
        new GeminiOptions { ApiKey = "test-key", Model = "gemini-test" });

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GenerateJson_BuildsRequestAndReturnsCandidateText()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"{\"a\":1}"}]}}]}"""));

        var text = await Client(handler).GenerateJsonAsync(
            "prompt", [new AiFile("image/png", [1, 2, 3])], """{"type":"object"}""", CancellationToken.None);

        Assert.Equal("""{"a":1}""", text);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("x-goog-api-key").Single());

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        var parts = body.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal("prompt", parts[0].GetProperty("text").GetString());
        Assert.Equal("image/png", parts[1].GetProperty("inlineData").GetProperty("mimeType").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), parts[1].GetProperty("inlineData").GetProperty("data").GetString());
        var config = body.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.Equal("object", config.GetProperty("responseJsonSchema").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, """{"candidates":[]}""")]
    public async Task ThrottledServerErrorOrNoCandidates_ThrowAiUnavailable(HttpStatusCode code, string body)
    {
        var client = Client(new StubHandler(_ => Json(code, body)));

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.GenerateJsonAsync("p", [], """{"type":"object"}""", CancellationToken.None));
    }
}
