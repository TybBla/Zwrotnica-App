using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

public sealed record ProfileDto(string Email, string? FullName, string? Address, string? Iban, PayoutPreference PreferredPayout);
