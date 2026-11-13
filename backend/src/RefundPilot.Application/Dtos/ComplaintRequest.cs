using System.ComponentModel.DataAnnotations;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>OneTimeIban is used for this letter only, unless SaveIbanToProfile is true.</summary>
public sealed record ComplaintRequest(
    PayoutPreference Payout,
    [MaxLength(50, ErrorMessage = "Numer rachunku jest za długi."), Iban] string? OneTimeIban,
    bool SaveIbanToProfile);
