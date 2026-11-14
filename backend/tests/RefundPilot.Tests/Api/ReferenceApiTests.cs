using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Api;

public sealed class ReferenceApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public ReferenceApiTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_IsAnonymousCacheableAndListsPkpIntercityWithFourCategories()
    {
        var response = await _factory.CreateCookieClient().GetAsync("/api/reference");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("max-age=3600", response.Headers.CacheControl!.ToString());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var pkp = json.GetProperty("carriers").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "PKP Intercity");
        Assert.Equal("Eligible", pkp.GetProperty("coverage").GetString());
        Assert.Equal(["EIP", "EIC", "IC", "TLK"],
            pkp.GetProperty("categories").EnumerateArray().Select(c => c.GetString()!).ToArray());
        var categories = json.GetProperty("categories").EnumerateArray().ToList();
        Assert.Equal(4, categories.Count);
        Assert.DoesNotContain(categories, c => c.GetProperty("code").GetString() == "IR");
        Assert.Contains(categories, c => c.GetProperty("code").GetString() == "TLK"
            && c.GetProperty("label").GetString() == "Twoje Linie Kolejowe");
        Assert.Equal(new CompensationOptions().MinimumPayout, json.GetProperty("minimumPayout").GetDecimal());
    }

    [Fact]
    public async Task Get_ListsEveryNotCoveredCarrierOfTheEligibilityRules()
    {
        var json = await _factory.CreateCookieClient().GetFromJsonAsync<JsonElement>("/api/reference");
        var listed = json.GetProperty("carriers").EnumerateArray().ToList();
        var eligibility = new EligibilityService();

        var notCovered = CarrierCatalog.Carriers.Where(c => c.Coverage == Verdict.NotCovered).ToList();
        Assert.True(notCovered.Count >= 11);
        foreach (var carrier in notCovered)
        {
            var item = Assert.Single(listed, c => c.GetProperty("name").GetString() == carrier.Name);
            Assert.Equal("NotCovered", item.GetProperty("coverage").GetString());
            Assert.Empty(item.GetProperty("categories").EnumerateArray());
            // The dropdown name itself must be recognised by the rules (single source, no drift).
            Assert.Equal(Verdict.NotCovered, eligibility.Assess(carrier.Name, null).Verdict);
        }
        Assert.Equal(CarrierCatalog.Carriers.Count, listed.Count);
    }
}
