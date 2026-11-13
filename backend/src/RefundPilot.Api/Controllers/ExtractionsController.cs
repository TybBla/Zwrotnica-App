using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/extractions")]
public sealed class ExtractionsController(IExtractionService extraction, IAiClient ai) : ControllerBase
{
    private const int MaxFiles = 4;
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private const long MaxRequestBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif", "application/pdf",
    };

    /// <summary>Reads ticket data from 1-4 uploaded files. Files are processed in memory and never stored.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<ExtractionDto>> Extract(
        [FromForm(Name = "files")] List<IFormFile>? files, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return Problem("AI wyłączone – wpisz dane ręcznie.", statusCode: StatusCodes.Status409Conflict);

        if (files is null || files.Count == 0)
            return Problem("Dodaj co najmniej jeden plik z biletem.", statusCode: StatusCodes.Status400BadRequest);
        if (files.Count > MaxFiles)
            return Problem($"Możesz dodać maksymalnie {MaxFiles} pliki.", statusCode: StatusCodes.Status400BadRequest);
        if (files.Any(f => f.Length == 0))
            return Problem("Jeden z plików jest pusty.", statusCode: StatusCodes.Status400BadRequest);
        if (files.Any(f => f.Length > MaxFileBytes))
            return Problem("Plik jest za duży (maksymalnie 5 MB).", statusCode: StatusCodes.Status400BadRequest);
        if (files.Any(f => !AllowedTypes.Contains(f.ContentType)))
            return Problem("Dozwolone formaty: JPEG, PNG, WebP, HEIC, HEIF i PDF.",
                statusCode: StatusCodes.Status400BadRequest);

        var aiFiles = new List<AiFile>(files.Count);
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, ct);
            aiFiles.Add(new AiFile(file.ContentType.ToLowerInvariant(), buffer.ToArray()));
        }

        try
        {
            return Ok(await extraction.ExtractAsync(aiFiles, ct));
        }
        catch (AiUnavailableException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
