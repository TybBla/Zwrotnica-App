using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cases/{id:int}/complaint")]
public sealed class ComplaintsController(IComplaintService complaints) : ControllerBase
{
    /// <summary>Generates the complaint letter text for one of the user's cases.</summary>
    [HttpPost]
    public async Task<ActionResult<ComplaintDto>> Generate(int id, ComplaintRequest request, CancellationToken ct)
    {
        var result = await complaints.GenerateAsync(id, request, ct);
        return result.Status switch
        {
            ComplaintStatus.Generated => Ok(result.Letter),
            ComplaintStatus.PriceRequired => Problem(
                "Podaj cenę biletu w sprawie, aby przygotować pismo.",
                statusCode: StatusCodes.Status409Conflict),
            _ => NotFound(),
        };
    }

    /// <summary>Renders the posted letter text as a PDF download (ownership checked; no AI call, no writes).</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> RenderPdf(int id, ComplaintPdfRequest request) =>
        await complaints.RenderPdfAsync(id, request) is { } pdf
            ? File(pdf, "application/pdf", $"reklamacja-{id}.pdf")
            : NotFound();
}
