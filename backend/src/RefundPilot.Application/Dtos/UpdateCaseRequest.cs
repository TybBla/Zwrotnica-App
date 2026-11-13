using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateCaseRequest(
    [Required(ErrorMessage = "Podaj przewoźnika."), MaxLength(200)] string Carrier,
    [MaxLength(50)] string? TrainCategory,
    [Required(ErrorMessage = "Podaj stację początkową."), MaxLength(200)] string FromStation,
    [Required(ErrorMessage = "Podaj stację docelową."), MaxLength(200)] string ToStation,
    [MaxLength(100)] string? TicketNumber,
    DateOnly TravelDate,
    [Range(0, 100000, ErrorMessage = "Cena biletu nie może być ujemna.")] decimal TicketPrice,
    IncidentType IncidentType,
    [Range(0, 10000, ErrorMessage = "Opóźnienie musi mieścić się w zakresie 0-10000 minut.")] int DelayMinutes,
    DelayCause? DelayCause,
    bool IsSeasonTicket,
    PayoutPreference PayoutPreference,
    [MaxLength(2000)] string? Notes);
