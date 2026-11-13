using RefundPilot.Domain;

namespace RefundPilot.Tests.Domain;

public sealed class CarrierCatalogTests
{
    [Theory]
    [InlineData("PKP INTERCITY", "PKP Intercity")]
    [InlineData("pkp ic", "PKP Intercity")]
    [InlineData("Intercity", null)]
    [InlineData("Polregio Intercity", "Polregio")]
    [InlineData("ÖBB Intercity", null)]
    [InlineData("SKM Poznań Intercity", null)]
    [InlineData("  PKP Intercity S.A. ", "PKP Intercity")]
    [InlineData("koleje slaskie", "Koleje Śląskie")]
    [InlineData("KOLEJE ŚLĄSKIE", "Koleje Śląskie")]
    [InlineData("PKP SKM w Trójmieście", null)]
    [InlineData("PKP SKM Trójmiasto", "PKP SKM Trójmiasto")]
    [InlineData("Nieznany Przewoźnik", null)]
    [InlineData(null, null)]
    public void FindCarrier_UsesTheEligibilityNormalization(string? text, string? expected) =>
        Assert.Equal(expected, CarrierCatalog.FindCarrier(text)?.Name);

    [Theory]
    [InlineData("ic", "IC")]
    [InlineData(" eip ", "EIP")]
    [InlineData("TLK", "TLK")]
    [InlineData("Regio", null)]
    [InlineData(null, null)]
    public void FindCategory_MatchesCodesCaseInsensitively(string? text, string? expected) =>
        Assert.Equal(expected, CarrierCatalog.FindCategory(text)?.Code);
}
