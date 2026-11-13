namespace RefundPilot.Domain;

public sealed record UserProfile(string? FullName, string? Address, string? Iban, PayoutPreference PreferredPayout);
