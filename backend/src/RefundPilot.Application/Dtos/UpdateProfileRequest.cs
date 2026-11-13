using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record UpdateProfileRequest(
    [MaxLength(200, ErrorMessage = "Imię i nazwisko może mieć maksymalnie 200 znaków.")] string? FullName,
    [MaxLength(300, ErrorMessage = "Adres może mieć maksymalnie 300 znaków.")] string? Address,
    [MaxLength(34, ErrorMessage = "Numer IBAN może mieć maksymalnie 34 znaki.")] string? Iban,
    PayoutPreference PreferredPayout);
