using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateProfileRequest(
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(200, ErrorMessage = "Imię i nazwisko może mieć maksymalnie 200 znaków."), AllowedCharacters(TextField.FullName)] string? FullName,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(300, ErrorMessage = "Adres może mieć maksymalnie 300 znaków."), AllowedCharacters(TextField.Address)] string? Address,
    [property: JsonConverter(typeof(CanonicalTextConverter))] [MaxLength(50, ErrorMessage = "Numer IBAN jest za długi."), Iban] string? Iban,
    PayoutPreference PreferredPayout);
