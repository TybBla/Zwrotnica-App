using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record AssessmentDto(
    Verdict Verdict, string Reason, decimal CompensationAmount, int Percent, bool BelowMinimum, bool PriceRequired,
    bool RefusalRisk);
