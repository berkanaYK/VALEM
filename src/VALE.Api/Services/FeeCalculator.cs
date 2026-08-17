namespace VALE.Api.Services;

public interface IFeeCalculator
{
    decimal Calculate(DateTimeOffset entryAt, DateTimeOffset exitAt, decimal hourlyRate);
}

public sealed class FeeCalculator : IFeeCalculator
{
    public decimal Calculate(DateTimeOffset entryAt, DateTimeOffset exitAt, decimal hourlyRate)
    {
        if (hourlyRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyRate));
        }

        var duration = exitAt <= entryAt ? TimeSpan.Zero : exitAt - entryAt;
        var billableHours = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes / 60d));
        return decimal.Round(billableHours * hourlyRate, 2, MidpointRounding.AwayFromZero);
    }
}

