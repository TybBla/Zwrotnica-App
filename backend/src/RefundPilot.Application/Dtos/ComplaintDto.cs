namespace RefundPilot.Application.Dtos;

/// <summary>MissingFields: Polish labels of placeholders left in the letter for the user to fill in.</summary>
public sealed record ComplaintDto(string Subject, string Body, bool UsedAi, IReadOnlyList<string> MissingFields);
