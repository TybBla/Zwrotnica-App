using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

/// <summary>Letters after the first one: kind is "reminder" (ponaglenie) or "utk-complaint" (skarga do Prezesa UTK).</summary>
[ApiController]
[Authorize]
[Route("api/cases/{id:int}/letters/{kind}")]
public sealed class FollowUpLettersController(IFollowUpLetterService letters) : ControllerBase
{
    /// <summary>Generates the letter text from the template. 409 when the case status does not allow it yet.</summary>
    [HttpPost]
    public async Task<ActionResult<ComplaintDto>> Generate(int id, string kind)
    {
        if (!FollowUpRules.TryParseKind(kind, out var parsed))
            return NotFound();
        var result = await letters.GenerateAsync(id, parsed);
        return result.Status == FollowUpStatus.Generated ? Ok(result.Letter) : Failure(result.Status, parsed);
    }

    /// <summary>Renders the posted letter text as a PDF download (ownership and availability checked; no writes).</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> RenderPdf(int id, string kind, ComplaintPdfRequest request)
    {
        if (!FollowUpRules.TryParseKind(kind, out var parsed))
            return NotFound();
        var result = await letters.RenderPdfAsync(id, parsed, request);
        var fileName = parsed == FollowUpKind.Reminder ? $"ponaglenie-{id}.pdf" : $"skarga-utk-{id}.pdf";
        return result.Status == FollowUpStatus.Generated ? File(result.Pdf!, "application/pdf", fileName) : Failure(result.Status, parsed);
    }

    private ActionResult Failure(FollowUpStatus status, FollowUpKind kind) => status == FollowUpStatus.NotAvailable
        ? Problem(FollowUpLetterService.UnavailableMessage(kind), statusCode: StatusCodes.Status409Conflict)
        : NotFound();
}
