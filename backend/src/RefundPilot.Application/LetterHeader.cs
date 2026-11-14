using System.Globalization;
using System.Text.RegularExpressions;

namespace RefundPilot.Application;

/// <summary>The "place, date" line of a Polish formal letter, e.g. "Gdańsk, 14 listopada 2026 r.".</summary>
public static partial class LetterHeader
{
    public const string PlacePlaceholder = "[miejscowość]";

    private static readonly string[] MonthsGenitive =
    [
        "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
        "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
    ];

    [GeneratedRegex(@"\d{2}-\d{3}\s*([^,]+)")]
    private static partial Regex AfterPostalCode();

    [GeneratedRegex(@"^[\p{L}][\p{L} \-]*$")]
    private static partial Regex CityLike();

    /// <summary>City from a free-text address: the text after a postal code, else a city-like last segment, else a placeholder.</summary>
    public static string PlaceFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return PlacePlaceholder;

        if (AfterPostalCode().Match(address) is { Success: true } m)
        {
            var city = m.Groups[1].Value.Trim();
            return CityLike().IsMatch(city) ? city : PlacePlaceholder;
        }

        var last = address.Split(',')[^1].Trim();
        return CityLike().IsMatch(last) ? last : PlacePlaceholder;
    }

    public static string PlaceAndDate(string place, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{place}, {date.Day} {MonthsGenitive[date.Month - 1]} {date.Year} r.");
}
