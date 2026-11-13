namespace RefundPilot.Application.Dtos;

public sealed record ExtractionDto(
    string? Carrier, string? TrainCategory, string? FromStation, string? ToStation, string? TicketNumber,
    decimal? TicketPrice, DateOnly? TravelDate, IReadOnlyList<string> MissingFields, bool UsedAi);
