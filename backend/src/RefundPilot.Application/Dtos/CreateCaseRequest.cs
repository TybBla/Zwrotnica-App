using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record CreateCaseRequest(
    [property: JsonConverter(typeof(CanonicalTextConverter))] [Required(ErrorMessage = "Podaj przewoźnika."), MaxLength(200), AllowedCharacters(TextField.Carrier)] string Carrier,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(50), AllowedCharacters(TextField.TrainCategory)] string? TrainCategory,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(200), AllowedCharacters(TextField.Station)] string? FromStation,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(200), AllowedCharacters(TextField.Station)] string? ToStation,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [Required(ErrorMessage = "Podaj numer biletu."), MaxLength(100), AllowedCharacters(TextField.TicketNumber)] string TicketNumber,
    [TravelDate] DateOnly TravelDate,
    [Range(0, 100000, ErrorMessage = "Cena biletu nie może być ujemna.")] decimal TicketPrice,
    IncidentType IncidentType,
    [Range(0, 10000, ErrorMessage = "Opóźnienie musi mieścić się w zakresie 0-10000 minut.")] int DelayMinutes,
    DelayCause? DelayCause,
    bool IsSeasonTicket,
    PayoutPreference PayoutPreference,
    [property: JsonConverter(typeof(CanonicalMultilineTextConverter))] [MaxLength(2000), AllowedCharacters(TextField.Notes)] string? Notes,
    [property: JsonConverter(typeof(CanonicalMultilineTextConverter))] [MaxLength(ExpenseRules.MaxDescription), AllowedCharacters(TextField.ExpenseDescription)] string? ExpenseDescription = null,
    [Range(0.01, ExpenseRules.MaxAmount, ErrorMessage = ExpenseRules.AmountMessage)] decimal? ExpenseAmount = null);
