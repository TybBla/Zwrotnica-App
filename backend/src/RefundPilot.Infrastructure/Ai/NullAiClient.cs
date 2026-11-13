using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure.Ai;

/// <summary>Used when no Gemini key is configured: AI features are off, the manual path remains.</summary>
public sealed class NullAiClient : IAiClient
{
    private const string Message = "AI wyłączone – wpisz dane ręcznie.";

    public bool IsConfigured => false;

    public Task<string> GenerateJsonAsync(
        string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct) =>
        throw new AiUnavailableException(Message);

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct) =>
        throw new AiUnavailableException(Message);
}
