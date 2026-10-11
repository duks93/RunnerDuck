namespace RunnerDuck.Core.Tests;

public sealed class RunnerSelectionTests
{
    private static readonly Runner Custom = new("custom-id", "Custom", isTemplate: false, FrameOrder.Ascending(2));

    private static Func<Runner, string> Loader(List<string> attempts, params string[] failingIDs) => runner =>
    {
        attempts.Add(runner.ID);
        if (failingIDs.Contains(runner.ID))
        {
            throw new RunnerLoadingException(runner.ID, new IOException("missing frame"));
        }
        return $"frames of {runner.ID}";
    };

    [Theory]
    [InlineData("duck")]
    [InlineData("custom-id")]
    public void Select_loads_stored_runner_and_keeps_its_id(string runnerID)
    {
        var settings = new AppSettings { RunnerID = runnerID };
        var attempts = new List<string>();
        var result = RunnerSelection.Select(settings, [Custom], Loader(attempts));
        Assert.Equal(runnerID, result.Runner.ID);
        Assert.Equal($"frames of {runnerID}", result.Frames);
        Assert.Null(result.Failure);
        Assert.Equal(runnerID, settings.RunnerID);
        Assert.Equal(new[] { runnerID }, attempts);
    }

    [Fact]
    public void Select_unknown_runner_falls_back_to_duck_and_stores_it()
    {
        var settings = new AppSettings { RunnerID = "deleted-custom-id" };
        var result = RunnerSelection.Select(settings, [Custom], Loader([]));
        Assert.Equal(Runner.Default, result.Runner);
        Assert.Null(result.Failure);
        Assert.Equal("duck", settings.RunnerID);
    }

    [Fact]
    public void Select_runner_with_broken_frames_falls_back_to_duck_and_stores_it()
    {
        var settings = new AppSettings { RunnerID = "custom-id" };
        var attempts = new List<string>();
        var result = RunnerSelection.Select(settings, [Custom], Loader(attempts, "custom-id"));
        Assert.Equal(Runner.Default, result.Runner);
        Assert.Equal("frames of duck", result.Frames);
        Assert.Equal("custom-id", result.Failure?.RunnerID);
        Assert.Equal("duck", settings.RunnerID);
        Assert.Equal(new[] { "custom-id", "duck" }, attempts);
    }

    [Fact]
    public void Select_reports_failure_when_default_runner_cannot_load()
    {
        var settings = new AppSettings { RunnerID = "unavailable-id" };
        var error = Assert.Throws<RunnerLoadingException>(() => RunnerSelection.Select(settings, [], Loader([], "unavailable-id", "duck")));
        Assert.Equal("duck", error.RunnerID);
        Assert.Equal("unavailable-id", settings.RunnerID);
    }

    [Fact]
    public void Switch_to_runner_updates_stored_id()
    {
        var settings = new AppSettings();
        var result = RunnerSelection.Switch(Custom, settings, Loader([]));
        Assert.Equal("custom-id", result.Runner.ID);
        Assert.Equal("frames of custom-id", result.Frames);
        Assert.Equal("custom-id", settings.RunnerID);
    }

    [Fact]
    public void Switch_failure_recovers_to_duck()
    {
        var settings = new AppSettings { RunnerID = "previous-custom-id" };
        var attempts = new List<string>();
        var result = RunnerSelection.Switch(Custom, settings, Loader(attempts, "custom-id"));
        Assert.Equal("custom-id", result.Failure?.RunnerID);
        Assert.Equal(Runner.Default, result.Runner);
        Assert.Equal("duck", settings.RunnerID);
        Assert.Equal(new[] { "custom-id", "duck" }, attempts);
    }
}

public sealed class RunnerTimerIntervalTests
{
    [Theory]
    [InlineData(0.0, false, 500)]
    [InlineData(0.5, false, 50)]
    [InlineData(1.0, false, 25)]
    [InlineData(0.0, true, 50)]
    [InlineData(0.5, true, 91)]
    [InlineData(1.0, true, 1000)]
    [InlineData(0.4, false, 63)]
    public void TimerInterval_rounds_frame_interval_to_milliseconds(double cpuRawValue, bool speedDecreasesUnderLoad, int expected)
    {
        var cpuInfo = new CPUInfo(Percentage: new Percentage(cpuRawValue));
        Assert.Equal(expected, RunnerSpeed.TimerIntervalMilliseconds(cpuInfo, speedDecreasesUnderLoad));
    }
}
