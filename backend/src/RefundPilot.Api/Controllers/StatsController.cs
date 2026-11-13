using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stats")]
public sealed class StatsController(ICaseService cases) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StatsDto>> Get() => Ok(await cases.GetStatsAsync());
}
