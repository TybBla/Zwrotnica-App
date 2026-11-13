using System.ComponentModel.DataAnnotations;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateCaseRequest(
    [Required(ErrorMessage = "Podaj przewoźnika."), MaxLength(200), AllowedCharacters(TextField.Carrier)] string Carrier,
    [MaxLength(50), AllowedCharacters(TextField.TrainCategory)] string? TrainCategory,
    [Required(ErrorMessage = "Podaj stację początkową."), MaxLength(200), AllowedCharacters(TextField.Station)] string FromStation,
    [Required(ErrorMessage = "Podaj stację docelową."), MaxLength(200), AllowedCharacters(TextField.Station)] string ToStation,
    [MaxLength(100), AllowedCharacters(TextField.TicketNumber)] string? TicketNumber,
    [TravelDate] DateOnly TravelDate,
    [Range(0, 100000, ErrorMessage = "Cena biletu nie może być ujemna.")] decimal TicketPrice,
    IncidentType IncidentType,
    [Range(0, 10000, ErrorMessage = "Opóźnienie musi mieścić się w zakresie 0-10000 minut.")] int DelayMinutes,
    DelayCause? DelayCause,
    bool IsSeasonTicket,
    PayoutPreference PayoutPreference,
    [MaxLength(2000), AllowedCharacters(TextField.Notes)] string? Notes);
