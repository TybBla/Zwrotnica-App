using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cases/{id:int}/complaint")]
public sealed class ComplaintsController(IComplaintService complaints, IComplaintPdfRenderer pdf) : ControllerBase
{
    /// <summary>Generates the complaint letter text for one of the user's cases.</summary>
    [HttpPost]
    public async Task<ActionResult<ComplaintDto>> Generate(int id, ComplaintRequest request, CancellationToken ct) =>
        await complaints.GenerateAsync(id, request, ct) is { } letter ? Ok(letter) : NotFound();

    /// <summary>Generates the same letter and returns it as a PDF download.</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf(int id, ComplaintRequest request, CancellationToken ct) =>
        await complaints.GenerateAsync(id, request, ct) is { } letter
            ? File(pdf.Render(letter), "application/pdf", $"reklamacja-{id}.pdf")
            : NotFound();
}
