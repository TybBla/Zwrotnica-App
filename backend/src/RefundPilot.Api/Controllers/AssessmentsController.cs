using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;

namespace RefundPilot.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/assessments")]
public sealed class AssessmentsController(IAssessmentService assessments) : ControllerBase
{
    [HttpPost]
    public ActionResult<AssessmentDto> Assess(AssessmentRequest request) => Ok(assessments.Assess(request));
}
