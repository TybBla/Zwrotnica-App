namespace RefundPilot.Application.Dtos;

/// <summary>Which follow-up letters the case allows now; PastDeadline: Sent and the carrier's deadline has passed.</summary>
public sealed record FollowUpsDto(bool Reminder, bool UtkComplaint, bool PastDeadline);
