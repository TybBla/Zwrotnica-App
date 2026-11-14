using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Services;

namespace RefundPilot.Tests.Api;

/// <summary>The hosted service is off in tests; the same ReminderService it runs is called directly.</summary>
public sealed class RemindersApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public RemindersApiTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SeededDemoCasePastItsDeadline_GetsOneReminderToTheDemoAccount()
    {
        var client = _factory.CreateCookieClient();
        await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = TestApiFactory.DemoEmail, password = TestApiFactory.DemoPassword });
        var cases = await (await client.GetAsync("/api/cases")).Content.ReadFromJsonAsync<JsonElement>();
        var overdue = cases.EnumerateArray().Single(c =>
            c.GetProperty("status").GetString() == "Sent"
            && c.GetProperty("responseDueDate").GetDateTime() < _factory.Time.GetUtcNow().UtcDateTime);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sent = await scope.ServiceProvider.GetRequiredService<IReminderService>().SendDueAsync(CancellationToken.None);

        Assert.Equal(1, sent);
        var message = Assert.Single(_factory.Emails.Sent);
        Assert.Equal(TestApiFactory.DemoEmail, message.To);
        Assert.Contains($"http://localhost:5173/sprawy/{overdue.GetProperty("id").GetInt32()}", message.Text);
    }
}
