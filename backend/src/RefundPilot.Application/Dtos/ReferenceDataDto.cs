using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>
/// Carrier/category dictionary for the UI. Carrier categories are category codes; empty means a regional carrier.
/// <paramref name="MinimumPayout"/> is the carrier's minimum payout in PLN (amounts below it get a warning).
/// </summary>
public sealed record ReferenceDataDto(
    IReadOnlyList<CarrierOption> Carriers, IReadOnlyList<CategoryOption> Categories, decimal MinimumPayout);

public sealed record CarrierOption(string Name, Verdict Coverage, IReadOnlyList<string> Categories);

public sealed record CategoryOption(string Code, string Label);
