using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>Outcome is null while the ticket price is missing (nothing can be stated yet).</summary>
public sealed record AssessmentDto(
    Verdict Verdict, string Reason, decimal CompensationAmount, int Percent, bool BelowMinimum, bool PriceRequired,
    bool RefusalRisk, OutcomeDto? Outcome);
