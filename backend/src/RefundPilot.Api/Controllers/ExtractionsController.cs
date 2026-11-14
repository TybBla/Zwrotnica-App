using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;
using RefundPilot.Application.Validation;

namespace RefundPilot.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/extractions")]
public sealed class ExtractionsController(IExtractionService extraction, IAiClient ai) : ControllerBase
{
    /// <summary>Reads ticket data from 1-4 uploaded files. Files are processed in memory and never stored.</summary>
    [HttpPost]
    [EnableRateLimiting("extractions")]
    [RequestSizeLimit(TicketFileRules.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketFileRules.MaxRequestBytes)]
    public async Task<ActionResult<ExtractionDto>> Extract(
        [FromForm(Name = "files")] List<IFormFile>? files, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return Problem("AI wyłączone – wpisz dane ręcznie.", statusCode: StatusCodes.Status409Conflict);

        var uploads = (files ?? []).Select(f => new TicketUpload(f.ContentType, f.Length, f.OpenReadStream)).ToList();
        if (TicketFileRules.Validate(uploads) is { } error)
            return Problem(error, statusCode: StatusCodes.Status400BadRequest);

        try
        {
            return Ok(await extraction.ExtractAsync(await TicketFileRules.ReadAsync(uploads, ct), ct));
        }
        catch (AiUnavailableException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
