using RefundPilot.Application;

namespace RefundPilot.Tests.Application;

public sealed class LetterHeaderTests
{
    [Theory]
    [InlineData("ul. Lipowa 12/4, 80-001 Gdańsk", "Gdańsk")]
    [InlineData("ul. Morska 1, 80-001 Gdańsk-Oliwa", "Gdańsk-Oliwa")]
    [InlineData("80-001   Gdańsk , woj. pomorskie", "Gdańsk")]
    [InlineData("ul. Lipowa 12/4, Kraków", "Kraków")]
    [InlineData("Kraków", "Kraków")]
    [InlineData(null, "[miejscowość]")]
    [InlineData("", "[miejscowość]")]
    [InlineData("   ", "[miejscowość]")]
    [InlineData("ul. Lipowa 12/4", "[miejscowość]")]
    [InlineData("ul. Lipowa 12/4, lok. 5", "[miejscowość]")]
    [InlineData("🚆🚆, <script>", "[miejscowość]")]
    [InlineData("80-001", "[miejscowość]")]
    public void PlaceFromAddress_ParsesCityOrFallsBack(string? address, string expected) =>
        Assert.Equal(expected, LetterHeader.PlaceFromAddress(address));

    [Theory]
    [InlineData("Zielona Góra Główna", "Zielona Góra")]
    [InlineData("Bielsko-Biała Główna", "Bielsko-Biała")]
    [InlineData("Kraków Płaszów", "Kraków")]
    [InlineData("Warszawa Lotnisko Chopina", "Warszawa")]
    [InlineData("Gdańsk Wrzeszcz", "Gdańsk")]
    [InlineData("Stargard", "Stargard")]
    [InlineData("Szczecin Główny", "Szczecin")]
    [InlineData("Warszawa Centralna", "Warszawa")]
    [InlineData("Łódź Fabryczna", "Łódź")]
    [InlineData("Gdańsk Przymorze-Uniwersytet", "Gdańsk")]
    [InlineData("Warszawa Zachodnia", "Warszawa")]
    [InlineData("kraków główny", "kraków")]
    [InlineData("Główna", "Główna")]                       // nothing left after stripping: the full name
    [InlineData("Lotnisko Chopina", "Lotnisko Chopina")]
    [InlineData("Kraków Gł. (peron 3)", "[miejscowość]")]  // not only letters, spaces and hyphens
    [InlineData(null, "[miejscowość]")]
    [InlineData("  ", "[miejscowość]")]
    public void PlaceFromStation_StripsStationDescriptors(string? station, string expected) =>
        Assert.Equal(expected, LetterHeader.PlaceFromStation(station));

    [Theory]
    [InlineData("ul. Lipowa 12/4, 80-001 Gdańsk", "Kraków Główny", "Gdynia Główna", "Gdańsk")] // profile address first
    [InlineData(null, "Kraków Główny", "Gdynia Główna", "Kraków")]                             // then the destination
    [InlineData("ul. Lipowa 12/4", "Kraków Główny", "Gdynia Główna", "Kraków")]                // address without a city
    [InlineData(null, null, "Gdynia Główna", "Gdynia")]                                        // then the origin
    [InlineData(null, "Kraków Gł. 3", "Gdynia Główna", "Gdynia")]                              // unusable destination
    [InlineData(null, null, null, "[miejscowość]")]                                            // only then the placeholder
    public void Place_FallsBackFromAddressToDestinationToOrigin(string? address, string? to, string? from, string expected) =>
        Assert.Equal(expected, LetterHeader.Place(address, to, from));

    [Fact]
    public void PlaceAndDate_UsesPolishGenitiveMonth() =>
        Assert.Equal("Gdańsk, 14 listopada 2026 r.", LetterHeader.PlaceAndDate("Gdańsk", new DateOnly(2026, 11, 14)));

    [Theory]
    [InlineData(1, "stycznia")] [InlineData(2, "lutego")] [InlineData(3, "marca")] [InlineData(4, "kwietnia")]
    [InlineData(5, "maja")] [InlineData(6, "czerwca")] [InlineData(7, "lipca")] [InlineData(8, "sierpnia")]
    [InlineData(9, "września")] [InlineData(10, "października")] [InlineData(11, "listopada")] [InlineData(12, "grudnia")]
    public void PlaceAndDate_AllMonths(int month, string name) =>
        Assert.Equal($"X, 3 {name} 2027 r.", LetterHeader.PlaceAndDate("X", new DateOnly(2027, month, 3)));
}
