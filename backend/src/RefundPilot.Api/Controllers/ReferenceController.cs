using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/reference")]
public sealed class ReferenceController(IReferenceDataService reference) : ControllerBase
{
    /// <summary>Static dictionary, safe to cache for an hour.</summary>
    [HttpGet]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public ActionResult<ReferenceDataDto> Get() => Ok(reference.Get());
}
