using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RefundPilot.Application.Dtos;

namespace RefundPilot.Tests.Application;

public sealed class TravelDateAttributeTests
{
    [Fact]
    public void TodayInWarsaw_IsAcceptedEvenWhenUtcStillYesterday()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero));
        var services = new ServiceCollection().AddSingleton<TimeProvider>(clock).BuildServiceProvider();
        var context = new ValidationContext(new object(), services, null);

        var result = new TravelDateAttribute().GetValidationResult(new DateOnly(2026, 11, 13), context);

        Assert.Equal(ValidationResult.Success, result);
    }
}
