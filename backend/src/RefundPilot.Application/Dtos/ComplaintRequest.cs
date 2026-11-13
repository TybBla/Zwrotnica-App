using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>OneTimeIban is used for this letter only, unless SaveIbanToProfile is true.</summary>
public sealed record ComplaintRequest(
    PayoutPreference Payout,
    [MaxLength(34, ErrorMessage = "Numer rachunku może mieć maksymalnie 34 znaki.")] string? OneTimeIban,
    bool SaveIbanToProfile);
