namespace RunnerDuck.Core.Tests;

public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, RelativeTimeUnit.Now, 0, false)]
    [InlineData(-0.5, RelativeTimeUnit.Now, 0, false)]
    [InlineData(-45, RelativeTimeUnit.Seconds, 45, false)]
    [InlineData(-59.9, RelativeTimeUnit.Seconds, 59, false)]
    [InlineData(-60, RelativeTimeUnit.Minutes, 1, false)]
    [InlineData(-3 * 60 - 30, RelativeTimeUnit.Minutes, 3, false)]
    [InlineData(-2 * 3600, RelativeTimeUnit.Hours, 2, false)]
    [InlineData(-36 * 3600, RelativeTimeUnit.Days, 1, false)]
    [InlineData(-10 * 86400, RelativeTimeUnit.Days, 10, false)]
    [InlineData(90, RelativeTimeUnit.Minutes, 1, true)]
    [InlineData(5 * 3600, RelativeTimeUnit.Hours, 5, true)]
    public void From_picks_largest_whole_unit(double offsetSeconds, RelativeTimeUnit unit, int amount, bool isFuture)
    {
        Assert.Equal(new RelativeTime(unit, amount, isFuture), RelativeTime.From(Now.AddSeconds(offsetSeconds), Now));
    }
}
