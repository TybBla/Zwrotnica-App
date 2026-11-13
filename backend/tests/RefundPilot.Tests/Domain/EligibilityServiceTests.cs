using RefundPilot.Domain;

namespace RefundPilot.Tests.Domain;

public class EligibilityServiceTests
{
    private readonly EligibilityService _service = new();

    [Theory]
    [InlineData("PKP Intercity", "EIC")]
    [InlineData("PKP Intercity", "IC")]
    [InlineData("pkp intercity", "tlk")]
    [InlineData("PKP Intercity", "IR")]
    [InlineData("PKP Intercity", "EIP")]
    public void Assess_PkpIntercityCategory_IsEligible(string carrier, string category)
    {
        Assert.Equal(Verdict.Eligible, _service.Assess(carrier, category).Verdict);
    }

    [Theory]
    [InlineData("Polregio")]
    [InlineData("POLREGIO")]
    [InlineData("Koleje Mazowieckie")]
    [InlineData("Koleje Śląskie")]
    [InlineData("koleje slaskie")]
    [InlineData("SKM")]
    [InlineData("WKD")]
    [InlineData("Łódzka Kolej Aglomeracyjna")]
    [InlineData("lka")]
    [InlineData("Arriva RP")]
    public void Assess_RegionalCarrier_IsNotCovered(string carrier)
    {
        var result = _service.Assess(carrier, null);

        Assert.Equal(Verdict.NotCovered, result.Verdict);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData("Deutsche Bahn", "ICE")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Assess_UnknownCarrier_IsUnknown(string? carrier, string? category)
    {
        Assert.Equal(Verdict.Unknown, _service.Assess(carrier, category).Verdict);
    }

    [Theory]
    [InlineData("PKP Intercity", "EuroNight międzynarodowy")]
    [InlineData("Deutsche Bahn", "international")]
    public void Assess_International_IsEligible(string carrier, string category)
    {
        Assert.Equal(Verdict.Eligible, _service.Assess(carrier, category).Verdict);
    }

    [Fact]
    public void Assess_PkpIntercityWithoutCategory_IsUnknown()
    {
        Assert.Equal(Verdict.Unknown, _service.Assess("PKP Intercity", null).Verdict);
    }
}
