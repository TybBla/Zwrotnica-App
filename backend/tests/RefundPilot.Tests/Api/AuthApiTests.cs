using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Api;

public sealed class AuthApiTests : IClassFixture<TestApiFactory>
{
    private const string Password = "Passw0rd!x";
    private readonly TestApiFactory _factory;

    public AuthApiTests(TestApiFactory factory) => _factory = factory;

    internal static async Task RegisterAndLoginAsync(HttpClient client, string email, string password = Password)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Profile_Returns401_WithoutRedirect()
    {
        var response = await _factory.CreateCookieClient().GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = _factory.CreateCookieClient();
        await RegisterAndLoginAsync(client, "logout@test.pl");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task SeededDemoUser_CanLogin_AndOwnsSixCases()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = TestApiFactory.DemoEmail, password = TestApiFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>()
            .FindByEmailAsync(TestApiFactory.DemoEmail);
        Assert.NotNull(user);
        var cases = await scope.ServiceProvider.GetRequiredService<ICaseRepository>().ListAsync(user.Id);
        Assert.Equal(6, cases.Count);
    }
}
