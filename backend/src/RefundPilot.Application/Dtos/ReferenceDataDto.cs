using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>Carrier/category dictionary for the UI. Carrier categories are category codes; empty means a regional carrier.</summary>
public sealed record ReferenceDataDto(IReadOnlyList<CarrierOption> Carriers, IReadOnlyList<CategoryOption> Categories);

public sealed record CarrierOption(string Name, Verdict Coverage, IReadOnlyList<string> Categories);

public sealed record CategoryOption(string Code, string Label);
