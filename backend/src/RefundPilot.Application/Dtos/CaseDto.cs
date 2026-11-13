using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record CaseDto(
    int Id, string Carrier, string? TrainCategory, string? FromStation, string? ToStation, string? TicketNumber,
    DateOnly TravelDate, decimal TicketPrice, IncidentType IncidentType, int DelayMinutes, DelayCause? DelayCause,
    bool IsSeasonTicket, Verdict Verdict, bool RefusalRisk, decimal CompensationAmount,
    PayoutPreference PayoutPreference, CaseStatus Status, DateTime? FiledAt, DateTime? ResponseDueDate,
    string? Notes, DateTime CreatedAt);
