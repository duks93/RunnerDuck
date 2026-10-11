using System.Text.Json;

namespace RunnerDuck.Core.Tests;

public sealed class RunnerTests
{
    [Theory]
    [InlineData(RunnerKind.Duck, "duck", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(RunnerKind.DuckFilled, "duck-filled", new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    public void RunnerKind_matches_mac_ids_and_frame_orders(RunnerKind kind, string id, int[] order)
    {
        Assert.Equal(id, kind.ID());
        Assert.Equal(order, kind.FrameOrder().Order);
    }

    [Theory]
    [InlineData(RunnerKind.Duck, 8)]
    [InlineData(RunnerKind.DuckFilled, 8)]
    public void RunnerKind_number_of_resources_matches_mac(RunnerKind kind, int expected)
    {
        Assert.Equal(expected, kind.NumberOfResources());
    }

    [Fact]
    public void FrameOrder_index_out_of_range_returns_zero()
    {
        Assert.Equal(0, FrameOrder.Swing[100]);
        Assert.Equal(3, FrameOrder.Swing[3]);
    }

    [Fact]
    public void ResourceNames_of_builtIn_runner_are_prefixed_with_runner_id()
    {
        Assert.Equal(
            Enumerable.Range(0, 8).Select(index => $"duck-frame-{index}"),
            new Runner(RunnerKind.Duck).ResourceNames()
        );
    }

    [Fact]
    public void ResourceNames_of_custom_runner_use_frame_prefix()
    {
        var runner = new Runner("custom-id", "Custom", isTemplate: false, FrameOrder.Custom([1, 0]));
        Assert.Equal(new[] { "frame-1", "frame-0" }, runner.ResourceNames());
        Assert.True(runner.IsCustom);
        Assert.Equal(new RunnerSource.Custom("Custom"), runner.Source);
    }

    [Fact]
    public void Default_runner_is_monochrome_duck()
    {
        Assert.Equal("duck", Runner.Default.ID);
        Assert.True(Runner.Default.IsTemplate);
        Assert.False(Runner.Default.IsCustom);
    }

    [Fact]
    public void Filled_duck_is_built_in_and_preserves_source_colors()
    {
        var runner = RunnerCatalog.Resolve("duck-filled", []);
        Assert.Equal("duck-filled", runner.ID);
        Assert.Equal(new RunnerSource.BuiltIn(RunnerKind.DuckFilled), runner.Source);
        Assert.False(runner.IsTemplate);
        Assert.False(runner.IsCustom);
    }

    [Fact]
    public void Runners_are_equal_by_id()
    {
        var first = new Runner("same", "First", isTemplate: true, FrameOrder.Ascending(2));
        var second = new Runner("same", "Second", isTemplate: false, FrameOrder.Ascending(3));
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new Runner("other", "First", isTemplate: true, FrameOrder.Ascending(2)));
    }

    [Fact]
    public void Custom_runner_round_trips_through_json()
    {
        var runner = new Runner("custom-id", "Custom", isTemplate: false, FrameOrder.Custom([2, 0, 1]));
        var json = JsonSerializer.Serialize(runner);
        Assert.Equal("""{"id":"custom-id","name":"Custom","isTemplate":false,"frameOrder":[2,0,1]}""", json);
        var decoded = JsonSerializer.Deserialize<Runner>(json)!;
        Assert.Equal(new RunnerSource.Custom("Custom"), decoded.Source);
        Assert.False(decoded.IsTemplate);
        Assert.Equal(new[] { 2, 0, 1 }, decoded.FrameOrder.Order);
    }

    [Fact]
    public void BuiltIn_runner_is_not_serialized()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Runner.Default));
    }

    [Theory]
    [InlineData(0.5, false, 10f)]
    [InlineData(1.0, false, 20f)]
    [InlineData(0.0, false, 1f)]
    [InlineData(0.5, true, 5.5f)]
    [InlineData(1.0, true, 0.5f)]
    [InlineData(0.0, true, 10f)]
    public void RunnerSpeed_matches_mac_formula(double cpuRawValue, bool speedDecreasesUnderLoad, float expected)
    {
        var cpuInfo = new CPUInfo(Percentage: new Percentage(cpuRawValue));
        Assert.Equal(expected, RunnerSpeed.Calculate(cpuInfo, speedDecreasesUnderLoad), precision: 4);
    }

    [Theory]
    [InlineData(-0.5, false, 1f)]
    [InlineData(1.5, false, 20f)]
    [InlineData(0.05, false, 1f)]
    [InlineData(0.06, false, 1.2f)]
    [InlineData(-0.5, true, 10f)]
    [InlineData(1.5, true, 0.5f)]
    public void RunnerSpeed_clamps_out_of_range_cpu_usage(double cpuRawValue, bool speedDecreasesUnderLoad, float expected)
    {
        var cpuInfo = new CPUInfo(Percentage: new Percentage(cpuRawValue));
        Assert.Equal(expected, RunnerSpeed.Calculate(cpuInfo, speedDecreasesUnderLoad), precision: 4);
    }

    [Fact]
    public void RunnerSpeed_is_minimum_when_cpu_info_is_missing()
    {
        Assert.Equal(1f, RunnerSpeed.Calculate(null));
        Assert.Equal(500d, RunnerSpeed.FrameIntervalMilliseconds(null));
    }

    [Fact]
    public void RunnerSpeed_is_maximum_when_cpu_info_is_missing_and_speed_decreases_under_load()
    {
        Assert.Equal(10f, RunnerSpeed.Calculate(null, speedDecreasesUnderLoad: true));
        Assert.Equal(50d, RunnerSpeed.FrameIntervalMilliseconds(null, speedDecreasesUnderLoad: true), precision: 6);
    }

    [Fact]
    public void FrameInterval_is_one_second_at_full_load_when_speed_decreases_under_load()
    {
        var cpuInfo = new CPUInfo(Percentage: new Percentage(1.0));
        Assert.Equal(1000d, RunnerSpeed.FrameIntervalMilliseconds(cpuInfo, speedDecreasesUnderLoad: true), precision: 6);
    }

    [Fact]
    public void FrameInterval_is_half_second_divided_by_speed()
    {
        var cpuInfo = new CPUInfo(Percentage: new Percentage(0.5));
        Assert.Equal(50d, RunnerSpeed.FrameIntervalMilliseconds(cpuInfo), precision: 6);
    }
}
