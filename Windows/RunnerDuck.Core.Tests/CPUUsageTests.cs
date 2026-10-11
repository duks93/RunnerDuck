namespace RunnerDuck.Core.Tests;

public sealed class CPUUsageTests
{
    [Fact]
    public void Calculate_splits_busy_time_into_system_and_user()
    {
        var previous = new SystemTimes(Idle: 1_000, Kernel: 2_000, User: 1_000);
        var current = new SystemTimes(Idle: 1_600, Kernel: 2_800, User: 1_200);
        Assert.Equal(
            new CPUInfo(
                Percentage: new Percentage(0.4),
                System: new Percentage(0.2),
                User: new Percentage(0.2),
                Idle: new Percentage(0.6)
            ),
            CPUUsage.Calculate(previous, current)
        );
    }

    [Fact]
    public void Calculate_reports_full_load_without_idle_time()
    {
        var info = CPUUsage.Calculate(new SystemTimes(0, 0, 0), new SystemTimes(0, 300, 700));
        Assert.Equal(100d, info.Percentage.Value);
        Assert.Equal(30d, info.System.Value);
        Assert.Equal(70d, info.User.Value);
        Assert.Equal(0d, info.Idle.Value);
    }

    [Fact]
    public void Calculate_reports_zero_usage_when_kernel_time_is_all_idle()
    {
        var info = CPUUsage.Calculate(new SystemTimes(0, 0, 0), new SystemTimes(800, 800, 0));
        Assert.Equal(new CPUInfo(new Percentage(0), new Percentage(0), new Percentage(0), new Percentage(1)), info);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-10, -20, -5)]
    [InlineData(-10, 500, 500)]
    [InlineData(10, -5, 500)]
    [InlineData(10, 500, -5)]
    public void Calculate_reports_idle_cpu_when_counters_did_not_advance(long idle, long kernel, long user)
    {
        var previous = new SystemTimes(100, 200, 300);
        var current = new SystemTimes(100 + idle, 200 + kernel, 300 + user);
        Assert.Equal(new CPUInfo(Idle: new Percentage(1)), CPUUsage.Calculate(previous, current));
    }

    [Fact]
    public void Calculate_feeds_runner_speed()
    {
        var info = CPUUsage.Calculate(new SystemTimes(0, 0, 0), new SystemTimes(500, 1_000, 0));
        Assert.Equal(10f, RunnerSpeed.Calculate(info));
    }
}
