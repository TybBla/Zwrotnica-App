namespace RefundPilot.Application.Dtos;

/// <summary>"Werdykt końcowy": the legal effect the complaint can be expected to have, in formal Polish.</summary>
public sealed record OutcomeDto(string Title, string Statement, string? LegalBasis, string? Deadline);
