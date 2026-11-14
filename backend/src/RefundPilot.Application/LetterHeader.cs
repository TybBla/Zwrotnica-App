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

    // Words that name the station within a town, never the town itself ("Kraków Płaszów", "Warszawa Lotnisko Chopina").
    private static readonly HashSet<string> StationDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Główny", "Główna", "Główne", "Centralna", "Centralny", "Wschodnia", "Zachodnia", "Wschód", "Zachód",
        "Północ", "Południe", "Lotnisko", "Chopina", "Płaszów", "Wrzeszcz", "Oliwa", "Fabryczna", "Kaliska",
        "Leśnica", "Miasto", "Osobowa", "Przymorze-Uniwersytet", "Stadion", "Śródmieście", "Zachodni", "Wschodni",
        "Gdański",
    };

    /// <summary>
    /// Town of a station: trailing descriptor words are stripped one by one ("Zielona Góra Główna" → "Zielona Góra");
    /// when nothing would remain, the full name. Placeholder for anything that is not letters, spaces and hyphens.
    /// </summary>
    public static string PlaceFromStation(string? station)
    {
        if (string.IsNullOrWhiteSpace(station))
            return PlacePlaceholder;
        var words = station.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var full = string.Join(' ', words);
        if (!CityLike().IsMatch(full))
            return PlacePlaceholder;

        while (words.Count > 0 && StationDescriptors.Contains(words[^1]))
            words.RemoveAt(words.Count - 1);
        return words.Count == 0 ? full : string.Join(' ', words);
    }

    /// <summary>Profile address city, else the destination's town, else the origin's town, else the placeholder.</summary>
    public static string Place(string? address, string? toStation, string? fromStation) =>
        new[] { PlaceFromAddress(address), PlaceFromStation(toStation), PlaceFromStation(fromStation) }
            .FirstOrDefault(place => place != PlacePlaceholder) ?? PlacePlaceholder;

    public static string PlaceAndDate(string place, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{place}, {date.Day} {MonthsGenitive[date.Month - 1]} {date.Year} r.");
}
