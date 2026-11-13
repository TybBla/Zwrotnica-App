namespace RefundPilot.Application.Dtos;

public sealed record StatsDto(decimal Recovered, decimal Pending, int TotalCases);
