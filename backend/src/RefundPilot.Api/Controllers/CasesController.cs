using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cases")]
public sealed class CasesController(ICaseService cases) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> List() => Ok(await cases.ListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CaseDto>> Get(int id) =>
        await cases.GetAsync(id) is { } found ? Ok(found) : NotFound();

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CreateCaseRequest request)
    {
        var created = await cases.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CaseDto>> Update(int id, UpdateCaseRequest request) =>
        await cases.UpdateAsync(id, request) is { } updated ? Ok(updated) : NotFound();

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<CaseDto>> SetStatus(int id, UpdateStatusRequest request) =>
        await cases.SetStatusAsync(id, request.Status!.Value) is { } updated ? Ok(updated) : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) => await cases.DeleteAsync(id) ? NoContent() : NotFound();
}
