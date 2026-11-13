using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IAssessmentService
{
    AssessmentDto Assess(AssessmentRequest request);
}

/// <summary>Single place combining eligibility, compensation and refusal-risk rules.</summary>
public sealed class AssessmentService(EligibilityService eligibility, CompensationCalculator calculator)
    : IAssessmentService
{
    public AssessmentDto Assess(AssessmentRequest request)
    {
        var refusalRisk = request.DelayCause is DelayCause.Weather or DelayCause.PersonsOnTrack;

        if (request.IsSeasonTicket)
            return new(Verdict.Unknown,
                "Bilety okresowe wymagają osobnego wyliczenia – nie podajemy kwoty automatycznie.",
                0m, 0, false, false, refusalRisk);

        var eligible = eligibility.Assess(request.Carrier, request.TrainCategory);
        if (eligible.Verdict != Verdict.Eligible)
            return new(eligible.Verdict, eligible.Reason, 0m, 0, false, false, refusalRisk);

        var compensation = calculator.Calculate(request.IncidentType, request.DelayMinutes, request.TicketPrice);
        return new(Verdict.Eligible, compensation.Reason, compensation.Amount, compensation.Percent,
            compensation.BelowMinimum, compensation.PriceRequired, refusalRisk);
    }
}
