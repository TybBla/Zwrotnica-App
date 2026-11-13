using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IProfileService profiles) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get() =>
        await profiles.GetAsync() is { } profile ? Ok(profile) : NotFound();

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request) =>
        await profiles.UpdateAsync(request) is { } profile ? Ok(profile) : NotFound();
}
