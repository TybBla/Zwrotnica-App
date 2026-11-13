using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string DefaultModel = "gemini-3.5-flash-lite";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = DefaultModel;
}

/// <summary>Minimal Gemini REST client (generateContent). The API key is sent only as a header and never logged.</summary>
public sealed class GeminiClient(HttpClient http, GeminiOptions options) : IAiClient
{
    private const string Unavailable = "Usługa AI jest chwilowo niedostępna. Spróbuj ponownie lub wpisz dane ręcznie.";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<string> GenerateJsonAsync(
        string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = prompt } };
        foreach (var file in files)
            parts.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = file.MimeType,
                    ["data"] = Convert.ToBase64String(file.Data),
                },
            });

        return await SendAsync(parts, new JsonObject
        {
            ["responseMimeType"] = "application/json",
            ["responseJsonSchema"] = JsonNode.Parse(jsonSchema),
        }, ct);
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct) =>
        SendAsync(new JsonArray { new JsonObject { ["text"] = prompt } }, null, ct);

    private async Task<string> SendAsync(JsonArray parts, JsonObject? generationConfig, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = parts } },
        };
        if (generationConfig is not null)
            body["generationConfig"] = generationConfig;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{options.Model}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("x-goog-api-key", options.ApiKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            // 429, 5xx and any other failure: the caller can only fall back to manual entry.
            if (!response.IsSuccessStatusCode)
                throw new AiUnavailableException(Unavailable);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return ExtractText(json);
        }
        catch (HttpRequestException)
        {
            throw new AiUnavailableException(Unavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) // HttpClient timeout
        {
            throw new AiUnavailableException(Unavailable);
        }
        catch (JsonException)
        {
            throw new AiUnavailableException(Unavailable);
        }
    }

    private static string ExtractText(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Object
            && json.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String)
            return text.GetString()!;

        throw new AiUnavailableException(Unavailable);
    }
}
