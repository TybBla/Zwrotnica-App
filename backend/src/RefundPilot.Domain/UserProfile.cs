namespace RefundPilot.Domain;

/// <summary>EmailReminders: the owner agreed to the one follow-up e-mail per case after the carrier's deadline.</summary>
public sealed record UserProfile(
    string? FullName, string? Address, string? Iban, PayoutPreference PreferredPayout, bool EmailReminders = true);
