using RefundPilot.Application.Abstractions;

namespace RefundPilot.Application.Validation;

/// <summary>One uploaded ticket file, independent of the web framework; <see cref="OpenRead"/> opens its content.</summary>
public sealed record TicketUpload(string ContentType, long Length, Func<Stream> OpenRead);

/// <summary>Limits for ticket uploads (count, size, type) with Polish messages, and reading the files for the AI client.</summary>
public static class TicketFileRules
{
    public const int MaxFiles = 4;
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif", "application/pdf",
    };

    /// <summary>The Polish error message for the first broken rule, or null when the files are acceptable.</summary>
    public static string? Validate(IReadOnlyCollection<TicketUpload> files)
    {
        if (files.Count == 0)
            return "Dodaj co najmniej jeden plik z biletem.";
        if (files.Count > MaxFiles)
            return $"Możesz dodać maksymalnie {MaxFiles} pliki.";
        if (files.Any(f => f.Length == 0))
            return "Jeden z plików jest pusty.";
        if (files.Any(f => f.Length > MaxFileBytes))
            return "Plik jest za duży (maksymalnie 5 MB).";
        if (files.Any(f => !AllowedTypes.Contains(f.ContentType)))
            return "Dozwolone formaty: JPEG, PNG, WebP, HEIC, HEIF i PDF.";
        return null;
    }

    /// <summary>Reads validated files into memory (never to disk) as AI client input.</summary>
    public static async Task<IReadOnlyList<AiFile>> ReadAsync(IReadOnlyList<TicketUpload> files, CancellationToken ct)
    {
        var result = new List<AiFile>(files.Count);
        foreach (var file in files)
        {
            await using var stream = file.OpenRead();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, ct);
            result.Add(new AiFile(file.ContentType.ToLowerInvariant(), buffer.ToArray()));
        }
        return result;
    }
}
