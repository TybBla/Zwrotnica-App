using RefundPilot.Domain;

namespace RefundPilot.Application.Dtos;

/// <summary>
/// Carrier/category dictionary for the UI. Carrier categories are category codes; empty means a regional carrier.
/// <paramref name="MinimumPayout"/> is the carrier's minimum payout in PLN (amounts below it get a warning).
/// <paramref name="Utk"/> is where a complaint to the rail regulator goes.
/// </summary>
public sealed record ReferenceDataDto(
    IReadOnlyList<CarrierOption> Carriers, IReadOnlyList<CategoryOption> Categories, decimal MinimumPayout,
    ComplaintOfficeDto Utk);

/// <summary>ComplaintUrl/ComplaintFormUrl/ComplaintChannels: verified official channels, null when none was verified.</summary>
public sealed record CarrierOption(
    string Name, Verdict Coverage, IReadOnlyList<string> Categories,
    string? ComplaintUrl = null, string? ComplaintFormUrl = null, string? ComplaintChannels = null);

public sealed record CategoryOption(string Code, string Label);

public sealed record ComplaintOfficeDto(string Name, string ComplaintUrl, string ComplaintChannels);
