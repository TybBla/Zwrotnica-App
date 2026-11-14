using RefundPilot.Domain;

namespace RefundPilot.Tests.Domain;

public class EligibilityServiceTests
{
    private readonly EligibilityService _service = new();

    [Theory]
    [InlineData("PKP Intercity", "EIC")]
    [InlineData("PKP Intercity", "IC")]
    [InlineData("pkp intercity", "tlk")]
    [InlineData("PKP Intercity", "EIP")]
    public void Assess_PkpIntercityCategory_IsEligible(string carrier, string category)
    {
        Assert.Equal(Verdict.Eligible, _service.Assess(carrier, category).Verdict);
    }

    [Theory]
    [InlineData("PKP Intercity", "IR")]
    [InlineData("PKP Intercity", "ir")]
    [InlineData("PKP Intercity", "InterRegio")]
    [InlineData("Deutsche Bahn", "IR")]
    public void Assess_InterRegio_IsUnknownBecausePolregioRunsIt(string carrier, string category)
    {
        var result = _service.Assess(carrier, category);

        Assert.Equal(Verdict.Unknown, result.Verdict);
        Assert.Contains("Polregio", result.Reason);
    }

    [Fact]
    public void Assess_InterRegioOfRegionalCarrier_StaysNotCovered()
    {
        Assert.Equal(Verdict.NotCovered, _service.Assess("Polregio", "IR").Verdict);
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

    [Theory]
    [InlineData("Slovak Intercity", "IC")]
    [InlineData("ÖBB Intercity", "IC")]
    [InlineData("Intercity Express", "IC")]
    [InlineData("Intercity", "IC")]
    public void Assess_ForeignOrBareIntercity_IsUnknown(string carrier, string category) =>
        Assert.Equal(Verdict.Unknown, _service.Assess(carrier, category).Verdict);

    [Fact]
    public void Assess_RegionalWinsOverIntercityWording() =>
        Assert.Equal(Verdict.NotCovered, _service.Assess("Polregio Intercity", "IC").Verdict);
}
