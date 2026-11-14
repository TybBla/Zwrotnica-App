using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IAssessmentService
{
    AssessmentDto Assess(AssessmentRequest request);
}

/// <summary>Single place combining eligibility, compensation and refusal-risk rules, and the final verdict built from them.</summary>
public sealed class AssessmentService(
    EligibilityService eligibility, CompensationCalculator calculator, CompensationOptions options)
    : IAssessmentService
{
    public AssessmentDto Assess(AssessmentRequest request)
    {
        var refusalRisk = request.DelayCause is DelayCause.Weather or DelayCause.PersonsOnTrack;
        var priceRequired = request.TicketPrice <= 0;

        if (request.IsSeasonTicket)
        {
            const string reason = "Bilety okresowe wymagają osobnego wyliczenia – nie podajemy kwoty automatycznie.";
            return new(Verdict.Unknown, reason, 0m, 0, false, false, refusalRisk,
                priceRequired ? null : FinalVerdict.Unclear(reason));
        }

        var eligible = eligibility.Assess(request.Carrier, request.TrainCategory);
        if (eligible.Verdict != Verdict.Eligible)
        {
            var outcome = priceRequired ? null
                : eligible.Verdict == Verdict.NotCovered ? FinalVerdict.NotCovered(request.ExpenseAmount)
                : FinalVerdict.Unclear(eligible.Reason);
            return new(eligible.Verdict, eligible.Reason, 0m, 0, false, false, refusalRisk, outcome);
        }

        var compensation = calculator.Calculate(request.IncidentType, request.DelayMinutes, request.TicketPrice);
        return new(Verdict.Eligible, compensation.Reason, compensation.Amount, compensation.Percent,
            compensation.BelowMinimum, compensation.PriceRequired, refusalRisk,
            compensation.PriceRequired ? null
                : FinalVerdict.Covered(request.IncidentType, compensation, refusalRisk, options.MinimumPayout));
    }
}
