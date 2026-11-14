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
