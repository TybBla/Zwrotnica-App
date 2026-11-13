using System.ComponentModel.DataAnnotations;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateProfileRequest(
    [MaxLength(200, ErrorMessage = "Imię i nazwisko może mieć maksymalnie 200 znaków."), AllowedCharacters(TextField.FullName)] string? FullName,
    [MaxLength(300, ErrorMessage = "Adres może mieć maksymalnie 300 znaków."), AllowedCharacters(TextField.Address)] string? Address,
    [MaxLength(50, ErrorMessage = "Numer IBAN jest za długi."), Iban] string? Iban,
    PayoutPreference PreferredPayout);
