using VALE.Api.Services;
using Xunit;

namespace VALE.Api.Tests;

public sealed class FeeCalculatorTests
{
    private readonly FeeCalculator _calculator = new();

    [Fact]
    public void Calculate_ChargesAtLeastOneHour()
    {
        var entry = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
        var amount = _calculator.Calculate(entry, entry.AddMinutes(10), 100m);

        Assert.Equal(100m, amount);
    }

    [Fact]
    public void Calculate_RoundsPartialHoursUp()
    {
        var entry = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
        var amount = _calculator.Calculate(entry, entry.AddMinutes(61), 100m);

        Assert.Equal(200m, amount);
    }

    [Fact]
    public void Calculate_RejectsNonPositiveRate()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(now, now.AddHours(1), 0m));
    }
}
