namespace RefundPilot.Application.Dtos;

/// <summary>
/// MissingFields: Polish labels of placeholders left in the letter for the user to fill in.
/// DeadlineStarted: this generation marked a New case as Sent, so the carrier's month (ResponseDueDate) starts today.
/// </summary>
public sealed record ComplaintDto(
    string Subject, string Body, bool UsedAi, IReadOnlyList<string> MissingFields,
    bool DeadlineStarted = false, DateTime? ResponseDueDate = null);
