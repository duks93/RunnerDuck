namespace RunnerDuck.Core.Tests;

public sealed class TrayToolTipTests
{
    [Fact]
    public void Compose_with_no_lines_returns_empty_text()
    {
        Assert.Equal("", TrayToolTip.Compose([], 127));
    }

    [Fact]
    public void Compose_joins_lines_in_input_order_with_newlines()
    {
        Assert.Equal("RunnerDuck\nCPU: 20%\nClaude Code: 5h 58% · Wk 5%", TrayToolTip.Compose(
            ["RunnerDuck", "CPU: 20%", "Claude Code: 5h 58% · Wk 5%"], 127));
    }

    [Fact]
    public void Compose_keeps_line_that_exactly_fits_limit()
    {
        var line = new string('x', 116);
        Assert.Equal($"RunnerDuck\n{line}", TrayToolTip.Compose(["RunnerDuck", line, "extra"], 127));
    }

    [Fact]
    public void Compose_drops_whole_overflowing_line_including_newline()
    {
        Assert.Equal("RunnerDuck", TrayToolTip.Compose(["RunnerDuck", new string('x', 117)], 127));
    }

    [Fact]
    public void Compose_stops_at_first_overflow_even_when_later_line_would_fit()
    {
        Assert.Equal("RunnerDuck\nCPU: 20%", TrayToolTip.Compose(
            ["RunnerDuck", "CPU: 20%", new string('x', 127), "short"], 127));
    }

    [Theory]
    [InlineData(4, "Runn")]
    [InlineData(10, "RunnerDuck")]
    [InlineData(0, "")]
    public void Compose_always_keeps_first_line_truncated_to_limit(int maximumLength, string expected)
    {
        Assert.Equal(expected, TrayToolTip.Compose(["RunnerDuck", "x"], maximumLength));
    }

    [Fact]
    public void Compose_truncates_long_first_line_at_127_characters()
    {
        Assert.Equal(new string('x', 127), TrayToolTip.Compose([new string('x', 128), "later"], 127));
    }

    [Fact]
    public void Compose_preserves_empty_first_line()
    {
        Assert.Equal("\nx", TrayToolTip.Compose(["", "x"], 2));
    }
}
