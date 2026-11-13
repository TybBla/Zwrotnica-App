using RefundPilot.Domain;

namespace RefundPilot.Tests.Domain;

public class CompensationCalculatorTests
{
    private readonly CompensationCalculator _calculator = new(new CompensationOptions());

    [Theory]
    [InlineData(59, 0, 0.00)]
    [InlineData(60, 25, 10.00)]
    [InlineData(119, 25, 10.00)]
    [InlineData(120, 50, 20.00)]
    [InlineData(-5, 0, 0.00)]
    public void Calculate_Delay_AppliesThresholds(int minutes, int percent, double amount)
    {
        var result = _calculator.Calculate(IncidentType.Delay, minutes, 40m);

        Assert.Equal(percent, result.Percent);
        Assert.Equal((decimal)amount, result.Amount);
    }

    [Fact]
    public void Calculate_Cancellation_RefundsFullPrice()
    {
        var result = _calculator.Calculate(IncidentType.Cancellation, 0, 40m);

        Assert.Equal(100, result.Percent);
        Assert.Equal(40.00m, result.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Calculate_NonPositivePrice_RequiresPrice(int price)
    {
        var result = _calculator.Calculate(IncidentType.Delay, 130, price);

        Assert.True(result.PriceRequired);
        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public void Calculate_AmountBelowMinimum_IsFlaggedButKept()
    {
        var result = _calculator.Calculate(IncidentType.Delay, 60, 20m);

        Assert.True(result.BelowMinimum);
        Assert.Equal(5.00m, result.Amount);
    }

    [Fact]
    public void Calculate_AmountAtMinimum_IsNotBelow()
    {
        var result = _calculator.Calculate(IncidentType.Delay, 120, 32m);

        Assert.False(result.BelowMinimum);
        Assert.Equal(16.00m, result.Amount);
    }

    [Fact]
    public void Calculate_RoundsHalfAwayFromZero()
    {
        var result = _calculator.Calculate(IncidentType.Delay, 60, 10.10m);

        Assert.Equal(2.53m, result.Amount);
    }
}
