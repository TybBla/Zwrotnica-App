using System.Globalization;
using System.Text;

namespace RefundPilot.Domain;

public sealed record CategoryInfo(string Code, string Label);

/// <summary>
/// A carrier as shown to users. <see cref="Aliases"/> are normalized (lower-case, no diacritics, single spaces)
/// whole-word keys used both to recognise the carrier in free text and to decide eligibility.
/// </summary>
public sealed record CarrierInfo(
    string Name, Verdict Coverage, IReadOnlyList<string> Aliases, IReadOnlyList<CategoryInfo> Categories);

/// <summary>Single source of truth for carriers and train categories (eligibility rules, reference API, extraction).</summary>
public static class CarrierCatalog
{
    public static IReadOnlyList<CategoryInfo> Categories { get; } =
    [
        new("EIP", "Express InterCity Premium"),
        new("EIC", "Express InterCity"),
        new("IC", "InterCity"),
        new("TLK", "Twoje Linie Kolejowe"),
        new("IR", "InterRegio"),
    ];

    // Poland exempts urban, suburban and regional services from the 25%/50% scheme
    // (Reg. (EU) 2021/782 art. 19 together with the national exemption).
    public static IReadOnlyList<CarrierInfo> Carriers { get; } =
    [
        new("PKP Intercity", Verdict.Eligible, ["pkp intercity", "pkp ic", "intercity"], Categories),
        Regional("Polregio", "polregio"),
        Regional("Koleje Mazowieckie", "koleje mazowieckie"),
        Regional("Koleje Śląskie", "koleje slaskie"),
        Regional("Koleje Dolnośląskie", "koleje dolnoslaskie"),
        Regional("Koleje Małopolskie", "koleje malopolskie"),
        Regional("Koleje Wielkopolskie", "koleje wielkopolskie"),
        Regional("SKM Warszawa", "skm warszawa"),
        Regional("PKP SKM Trójmiasto", "pkp skm trojmiasto"),
        Regional("WKD", "wkd"),
        Regional("Łódzka Kolej Aglomeracyjna", "lodzka kolej aglomeracyjna", "lka"),
        Regional("Arriva RP", "arriva rp"),
    ];

    // Short regional markers that identify a regional operator but not a specific dictionary entry.
    private static readonly string[] GenericRegionalKeys = ["skm"];

    /// <summary>Markers of cross-border services; these are not a carrier in the dropdown.</summary>
    public static IReadOnlyList<string> InternationalMarkers { get; } = ["international", "miedzynarodow"];

    private static CarrierInfo Regional(string name, params string[] aliases) =>
        new(name, Verdict.NotCovered, aliases, []);

    /// <summary>The dictionary carrier named in <paramref name="text"/> (longest matching alias wins), or null.</summary>
    public static CarrierInfo? FindCarrier(string? text)
    {
        var normalized = Normalize(text);
        return Carriers
            .Select(c => (Carrier: c, Length: c.Aliases.Where(a => ContainsKey(normalized, a)).Select(a => a.Length).DefaultIfEmpty(0).Max()))
            .Where(x => x.Length > 0)
            .OrderByDescending(x => x.Length)
            .Select(x => x.Carrier)
            .FirstOrDefault();
    }

    /// <summary>The dictionary category whose code equals <paramref name="text"/> (case-insensitive), or null.</summary>
    public static CategoryInfo? FindCategory(string? text)
    {
        var normalized = Normalize(text).Trim();
        return Categories.FirstOrDefault(c => c.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsRegional(string normalizedCarrier) =>
        Carriers.Where(c => c.Coverage == Verdict.NotCovered).SelectMany(c => c.Aliases).Concat(GenericRegionalKeys)
            .Any(key => ContainsKey(normalizedCarrier, key));

    internal static bool IsPkpIntercity(string normalizedCarrier) =>
        Carriers.Where(c => c.Coverage == Verdict.Eligible).SelectMany(c => c.Aliases)
            .Any(key => ContainsKey(normalizedCarrier, key));

    internal static bool HasIntercityCategory(string normalizedCategory) =>
        Categories.Any(c => ContainsKey(normalizedCategory, Normalize(c.Code).Trim()));

    internal static bool HasInternationalMarker(string normalized) =>
        InternationalMarkers.Any(normalized.Contains);

    private static bool ContainsKey(string normalized, string key) => normalized.Contains($" {key} ");

    /// <summary>Lower-case, strip diacritics, keep letters/digits only; padded so keys match whole words.</summary>
    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return " ";

        var decomposed = value.ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(" ");
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return builder.Append(' ').ToString();
    }
}
