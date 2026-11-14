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

    /// <summary>Only addresses verified on the carriers' own sites (2026-11-14); every other carrier gets no link.</summary>
    private static readonly Dictionary<string, string> VerifiedComplaintUrls = new()
    {
        ["PKP Intercity"] = "https://www.intercity.pl/pl/site/dla-pasazera/informacje/reklamacje-i-skargi/podstawowe-informacje-na-temat-rozpatrywania-skarg,-reklamacji-i-wnioskow.html",
        ["Polregio"] = "https://polregio.pl/pl/obsluga-klienta/reklamacje-skargi-wnioski/",
        ["Koleje Mazowieckie"] = "https://www.mazowieckie.com.pl/",
        ["Koleje Śląskie"] = "https://www.kolejeslaskie.pl/obsluga-podroznych/pomocne-informacje/reklamacje-skargi-i-wnioski/",
        ["Koleje Dolnośląskie"] = "https://kolejedolnoslaskie.pl/skargi-i-reklamacje/",
        ["Koleje Małopolskie"] = "https://www.kolejemalopolskie.com.pl/pl/dla-pasazera/skargi-i-reklamacje",
        ["Koleje Wielkopolskie"] = "https://koleje-wielkopolskie.com.pl/dla-pasazera/skargi-i-reklamacje/",
        ["SKM Warszawa"] = "https://www.skm.warszawa.pl/",
        ["PKP SKM Trójmiasto"] = "https://www.skm.pkp.pl/dla-pasazera/skargi-reklamacje-zapytania",
        ["WKD"] = "https://www.wkd.com.pl/",
        ["Łódzka Kolej Aglomeracyjna"] = "https://lka.lodzkie.pl/",
        ["Arriva RP"] = "https://arriva.pl/859/skargi-i-reklamacje",
    };

    [Fact]
    public async Task Get_CarriesOnlyVerifiedComplaintLinks()
    {
        var json = await _factory.CreateCookieClient().GetFromJsonAsync<JsonElement>("/api/reference");
        var carriers = json.GetProperty("carriers").EnumerateArray().ToList();

        foreach (var carrier in carriers)
        {
            var name = carrier.GetProperty("name").GetString()!;
            var url = carrier.GetProperty("complaintUrl");
            if (VerifiedComplaintUrls.TryGetValue(name, out var expected))
                Assert.Equal(expected, url.GetString());
            else
                Assert.Equal(JsonValueKind.Null, url.ValueKind);
        }

        var pkp = carriers.Single(c => c.GetProperty("name").GetString() == "PKP Intercity");
        Assert.Equal("https://www.intercity.pl/pl/dokumenty/formularze/Formularz-reklamacyjny-03-2026-pl.pdf",
            pkp.GetProperty("complaintFormUrl").GetString());
        Assert.Contains("Al. Jerozolimskie 142 A", pkp.GetProperty("complaintChannels").GetString());
        // A carrier checked only by its home page gets no channel description that was not verified.
        var km = carriers.Single(c => c.GetProperty("name").GetString() == "Koleje Mazowieckie");
        Assert.Equal(JsonValueKind.Null, km.GetProperty("complaintChannels").ValueKind);
        Assert.Equal(JsonValueKind.Null, km.GetProperty("complaintFormUrl").ValueKind);

        var utk = json.GetProperty("utk");
        Assert.Equal("Prezes Urzędu Transportu Kolejowego", utk.GetProperty("name").GetString());
        Assert.Equal("https://utk.gov.pl/pl/urzad/kontakt", utk.GetProperty("complaintUrl").GetString());
        Assert.Contains("pasazer@utk.gov.pl", utk.GetProperty("complaintChannels").GetString());
        Assert.Contains("Al. Jerozolimskie 134, 02-305 Warszawa", utk.GetProperty("complaintChannels").GetString());
    }
}
