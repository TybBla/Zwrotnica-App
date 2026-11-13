namespace RefundPilot.Application.Abstractions;

public sealed record AiFile(string MimeType, byte[] Data);

/// <summary>Thrown when the AI provider is unreachable, throttled or returns something unusable.</summary>
public sealed class AiUnavailableException(string message) : Exception(message);

public interface IAiClient
{
    bool IsConfigured { get; }

    Task<string> GenerateJsonAsync(
        string prompt, IReadOnlyList<AiFile> files, string jsonSchema, CancellationToken ct);

    Task<string> GenerateTextAsync(string prompt, CancellationToken ct);
}
