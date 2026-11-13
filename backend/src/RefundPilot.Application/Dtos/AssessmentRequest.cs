using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record AssessmentRequest(
    [MaxLength(200)] string? Carrier,
    [MaxLength(50)] string? TrainCategory,
    IncidentType IncidentType,
    [Range(0, 10000, ErrorMessage = "Opóźnienie musi mieścić się w zakresie 0-10000 minut.")] int DelayMinutes,
    [Range(0, 100000, ErrorMessage = "Cena biletu nie może być ujemna.")] decimal TicketPrice,
    DelayCause? DelayCause,
    bool IsSeasonTicket);
