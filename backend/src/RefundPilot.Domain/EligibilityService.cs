using System.Globalization;
using System.Text;

namespace RefundPilot.Domain;

public sealed record EligibilityResult(Verdict Verdict, string Reason);

public sealed class EligibilityService
{
    // Poland exempts urban, suburban and regional services from the 25%/50% scheme
    // (Reg. (EU) 2021/782 art. 19 together with the national exemption).
    private static readonly string[] RegionalCarriers =
    [
        "polregio", "koleje mazowieckie", "koleje slaskie", "koleje dolnoslaskie",
        "koleje malopolskie", "koleje wielkopolskie", "skm", "wkd",
        "lodzka kolej aglomeracyjna", "lka", "arriva rp",
    ];

    private static readonly string[] IntercityCategories = ["eip", "eic", "ic", "tlk", "ir"];

    private static readonly string[] InternationalMarkers = ["international", "miedzynarodow"];

    public EligibilityResult Assess(string? carrier, string? trainCategory)
    {
        var carrierText = Normalize(carrier);
        var categoryText = Normalize(trainCategory);

        if (ContainsAny(carrierText, RegionalCarriers))
            return new(Verdict.NotCovered,
                "Pociągi regionalne, miejskie i podmiejskie są w Polsce wyłączone z rekompensat 25%/50%.");

        if (InternationalMarkers.Any(m => carrierText.Contains(m) || categoryText.Contains(m)))
            return new(Verdict.Eligible, "Połączenie międzynarodowe objęte rozporządzeniem (UE) 2021/782.");

        if (ContainsAny(carrierText, ["pkp intercity"]) && ContainsAny(categoryText, IntercityCategories))
            return new(Verdict.Eligible, "Pociąg PKP Intercity objęty rekompensatą 25%/50% ceny biletu.");

        return new(Verdict.Unknown, "Nie udało się jednoznacznie określić przewoźnika lub kategorii pociągu.");
    }

    private static bool ContainsAny(string text, IEnumerable<string> keys) =>
        keys.Any(key => text.Contains($" {key} "));

    // Lower-case, strip diacritics, keep letters/digits only; padded so keys match whole words.
    private static string Normalize(string? value)
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
