using System.Globalization;

namespace RunnerDuck.Core.Tests;

public sealed class SystemMetricsSamplerTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Reset_during_sample_prevents_stale_network_baseline(bool pauseAtCPU, bool disconnected)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var shouldPause = false;
        var now = DateTimeOffset.UnixEpoch;
        NetworkCounters? counters = new("wifi", 100, 200, NetworkInterface.WiFi, null);
        void Pause()
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Sample was not released");
            }
        }
        var sampler = new SystemMetricsSampler(new SystemMetricsEnvironment
        {
            SampleCPU = () => { if (shouldPause && pauseAtCPU) Pause(); return null; },
            SampleMemory = () => null,
            SampleStorage = () => null,
            SampleBattery = () => null,
            SampleNetwork = () =>
            {
                var captured = counters;
                if (shouldPause && !pauseAtCPU) Pause();
                return captured;
            },
            Now = () => now,
        });
        var configuration = new SystemMetricsConfiguration();
        sampler.Sample(configuration);
        now = now.AddSeconds(5);
        counters = disconnected ? null : new("wifi", 1_000, 2_000, NetworkInterface.WiFi, null);
        shouldPause = true;
        var pending = Task.Run(() => sampler.Sample(configuration));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            sampler.Reset();
        }
        finally
        {
            release.Set();
        }
        await pending;
        shouldPause = false;
        now = now.AddHours(8);
        counters = new("wifi", 100_000, 200_000, NetworkInterface.WiFi, null);
        var resumed = sampler.Sample(configuration).NetworkInfo!;
        Assert.Equal(ByteData.Zero, resumed.Upload);
        Assert.Equal(ByteData.Zero, resumed.Download);
        now = now.AddSeconds(2);
        counters = counters with { BytesSent = 102_000, BytesReceived = 204_000 };
        var next = sampler.Sample(configuration).NetworkInfo!;
        Assert.Equal(new ByteData(1_000), next.Upload);
        Assert.Equal(new ByteData(2_000), next.Download);
    }

    private sealed class FakeSystem
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        public NetworkCounters? Network { get; set; } = new("wifi", 0, 0, NetworkInterface.WiFi, "192.168.0.10");
        public BatteryInfo? Battery { get; set; } = new(new Percentage(0.5), IsInstalled: true);
        public List<string> Sampled { get; } = [];

        public SystemMetricsEnvironment Environment() => new()
        {
            SampleCPU = () => Record("cpu", new CPUInfo(Percentage: new Percentage(0.3))),
            SampleMemory = () => Record("memory", new MemoryInfo(Percentage: new Percentage(0.6))),
            SampleStorage = () => Record("storage", new StorageInfo(Percentage: new Percentage(0.7))),
            SampleBattery = () => Record("battery", Battery),
            SampleNetwork = () => Record("network", Network),
            Now = () => Now,
        };

        private T Record<T>(string name, T value)
        {
            Sampled.Add(name);
            return value;
        }
    }

    [Fact]
    public void Changing_memory_monitoring_preserves_network_baseline_and_history()
    {
        var system = new FakeSystem();
        var sampler = new SystemMetricsSampler(system.Environment());
        var metrics = new Metrics();
        metrics.Update(sampler.Sample(new SystemMetricsConfiguration()));
        var before = metrics.SystemInfoBundle;
        var cpuHistory = metrics.CPURingBuffer.Values.ToArray();
        var memoryHistory = metrics.MemoryRingBuffer.Values.ToArray();
        var configuration = new SystemMetricsConfiguration { MonitorsMemory = false };
        metrics.ApplyConfiguration(configuration);
        Assert.Equal(before with { MemoryInfo = null }, metrics.SystemInfoBundle);
        Assert.Equal(cpuHistory, metrics.CPURingBuffer.Values);
        Assert.Equal(memoryHistory, metrics.MemoryRingBuffer.Values);
        system.Now = system.Now.AddSeconds(5);
        system.Network = new("wifi", 5_000, 10_000, NetworkInterface.WiFi, null);
        var next = sampler.Sample(configuration);
        Assert.Null(next.MemoryInfo);
        Assert.Equal(new ByteData(1_000), next.NetworkInfo!.Upload);
        Assert.Equal(new ByteData(2_000), next.NetworkInfo.Download);
    }

    [Fact]
    public void Disabling_optional_metrics_hides_values_without_recording_samples()
    {
        var system = new FakeSystem();
        var metrics = new Metrics();
        metrics.Update(new SystemMetricsSampler(system.Environment()).Sample(new SystemMetricsConfiguration()));
        var cpu = metrics.SystemInfoBundle.CPUInfo;
        var history = metrics.CPURingBuffer.Values.ToArray();
        metrics.ApplyConfiguration(new SystemMetricsConfiguration
        {
            MonitorsMemory = false, MonitorsStorage = false, MonitorsBattery = false, MonitorsNetwork = false,
        });
        Assert.Equal(new SystemInfoBundle(CPUInfo: cpu), metrics.SystemInfoBundle);
        Assert.Equal(history, metrics.CPURingBuffer.Values);
    }

    [Fact]
    public void Sample_reads_only_monitored_metrics()
    {
        var system = new FakeSystem();
        var sut = new SystemMetricsSampler(system.Environment());
        var bundle = sut.Sample(new SystemMetricsConfiguration { MonitorsMemory = false, MonitorsBattery = false });
        Assert.Equal(new[] { "cpu", "storage", "network" }, system.Sampled);
        Assert.NotNull(bundle.CPUInfo);
        Assert.Null(bundle.MemoryInfo);
        Assert.NotNull(bundle.StorageInfo);
        Assert.Null(bundle.BatteryInfo);
        Assert.NotNull(bundle.NetworkInfo);
    }

    [Fact]
    public void Sample_reads_nothing_but_cpu_when_all_optional_metrics_are_off()
    {
        var system = new FakeSystem();
        var bundle = new SystemMetricsSampler(system.Environment()).Sample(
            new SystemMetricsConfiguration { MonitorsMemory = false, MonitorsStorage = false, MonitorsBattery = false, MonitorsNetwork = false }
        );
        Assert.Equal(new[] { "cpu" }, system.Sampled);
        Assert.Equal(new SystemInfoBundle(CPUInfo: new CPUInfo(Percentage: new Percentage(0.3))), bundle);
    }

    [Fact]
    public void Sample_reports_missing_battery_as_not_installed()
    {
        var system = new FakeSystem { Battery = null };
        var bundle = new SystemMetricsSampler(system.Environment()).Sample(new SystemMetricsConfiguration());
        Assert.Equal(new BatteryInfo(), bundle.BatteryInfo);
    }

    [Fact]
    public void Failed_cpu_sample_is_not_recorded_as_zero_usage()
    {
        var sampler = new SystemMetricsSampler(new SystemMetricsEnvironment
        {
            SampleCPU = () => null,
            SampleMemory = () => null,
            SampleStorage = () => null,
            SampleBattery = () => null,
            SampleNetwork = () => null,
            Now = () => DateTimeOffset.UnixEpoch,
        });
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(CPUInfo: new CPUInfo(Percentage: new Percentage(0.5))));
        var history = metrics.CPURingBuffer.Values;
        var bundle = sampler.Sample(new SystemMetricsConfiguration { MonitorsMemory = false, MonitorsStorage = false, MonitorsBattery = false, MonitorsNetwork = false });
        metrics.Update(bundle);
        Assert.Null(bundle.CPUInfo);
        Assert.Equal(history, metrics.CPURingBuffer.Values);
    }

    [Fact]
    public void Network_speed_is_zero_on_first_sample_then_computed_from_counter_delta()
    {
        var system = new FakeSystem();
        var sut = new SystemMetricsSampler(system.Environment());
        var configuration = new SystemMetricsConfiguration();
        Assert.Equal(
            new NetworkInfo(true, NetworkInterface.WiFi, "192.168.0.10", ByteData.Zero, ByteData.Zero),
            sut.Sample(configuration).NetworkInfo
        );
        system.Now = system.Now.AddSeconds(5);
        system.Network = new("wifi", 5_000, 50_000, NetworkInterface.WiFi, "192.168.0.10");
        Assert.Equal(
            new NetworkInfo(true, NetworkInterface.WiFi, "192.168.0.10", new ByteData(1_000), new ByteData(10_000)),
            sut.Sample(configuration).NetworkInfo
        );
    }

    [Fact]
    public void Network_speed_resets_when_adapter_changes_or_monitoring_restarts()
    {
        var system = new FakeSystem();
        var sut = new SystemMetricsSampler(system.Environment());
        var configuration = new SystemMetricsConfiguration();
        sut.Sample(configuration);
        system.Now = system.Now.AddSeconds(1);
        system.Network = new("ethernet", 1_000_000, 1_000_000, NetworkInterface.Ethernet, null);
        var switched = sut.Sample(configuration).NetworkInfo!;
        Assert.Equal(ByteData.Zero, switched.Upload);
        Assert.Equal(ByteData.Zero, switched.Download);
        sut.Sample(new SystemMetricsConfiguration { MonitorsNetwork = false });
        system.Now = system.Now.AddSeconds(1);
        system.Network = new("ethernet", 2_000_000, 3_000_000, NetworkInterface.Ethernet, null);
        Assert.Equal(ByteData.Zero, sut.Sample(configuration).NetworkInfo!.Download);
    }

    [Fact]
    public void Network_speed_restarts_from_zero_after_reset_and_reconnection()
    {
        var system = new FakeSystem();
        var sut = new SystemMetricsSampler(system.Environment());
        var configuration = new SystemMetricsConfiguration();
        sut.Sample(configuration);
        sut.Reset();
        system.Now = system.Now.AddHours(8);
        system.Network = new("wifi", 9_000_000, 9_000_000, NetworkInterface.WiFi, "192.168.0.10");
        Assert.Equal(
            new NetworkInfo(true, NetworkInterface.WiFi, "192.168.0.10", ByteData.Zero, ByteData.Zero),
            sut.Sample(configuration).NetworkInfo
        );
        system.Network = null;
        Assert.Equal(new NetworkInfo(), sut.Sample(configuration).NetworkInfo);
        system.Now = system.Now.AddSeconds(1);
        system.Network = new("wifi", 9_500_000, 9_900_000, NetworkInterface.WiFi, "192.168.0.10");
        var reconnected = sut.Sample(configuration).NetworkInfo!;
        Assert.Equal(ByteData.Zero, reconnected.Upload);
        Assert.Equal(ByteData.Zero, reconnected.Download);
        system.Now = system.Now.AddSeconds(2);
        system.Network = new("wifi", 9_502_000, 9_904_000, NetworkInterface.WiFi, "192.168.0.10");
        var recovered = sut.Sample(configuration).NetworkInfo!;
        Assert.Equal(new ByteData(1_000), recovered.Upload);
        Assert.Equal(new ByteData(2_000), recovered.Download);
    }

    [Fact]
    public void Network_counter_reset_reports_zero_in_both_directions()
    {
        var system = new FakeSystem { Network = new("wifi", 5_000, 5_000, NetworkInterface.WiFi, null) };
        var sut = new SystemMetricsSampler(system.Environment());
        var configuration = new SystemMetricsConfiguration();
        sut.Sample(configuration);
        system.Now = system.Now.AddSeconds(1);
        system.Network = new("wifi", 100, 200, NetworkInterface.WiFi, null);
        var info = sut.Sample(configuration).NetworkInfo!;
        Assert.Equal(ByteData.Zero, info.Upload);
        Assert.Equal(ByteData.Zero, info.Download);
    }

    [Fact]
    public void Network_without_active_adapter_is_disconnected()
    {
        var system = new FakeSystem { Network = null };
        Assert.Equal(new NetworkInfo(), new SystemMetricsSampler(system.Environment()).Sample(new SystemMetricsConfiguration()).NetworkInfo);
    }

    [Theory]
    [InlineData(0, 1_000, 2, 500)]
    [InlineData(1_000, 1_000, 2, 0)]
    [InlineData(2_000, 1_000, 2, 0)]
    [InlineData(0, 1_000, 0, 0)]
    public void NetworkSpeed_divides_counter_delta_by_elapsed_seconds(long previous, long current, int seconds, double expected)
    {
        Assert.Equal(new ByteData(expected), NetworkSpeed.BytesPerSecond(previous, current, TimeSpan.FromSeconds(seconds)));
    }
}

public sealed class DashboardRowsTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Build_lists_monitored_metrics_with_details_and_graphs()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(
            CPUInfo: new CPUInfo(new Percentage(0.424), new Percentage(0.1), new Percentage(0.324), new Percentage(0.576)),
            MemoryInfo: new MemoryInfo(new Percentage(0.6), new ByteData(9.6e9), new ByteData(6.4e9), new ByteData(16e9)),
            StorageInfo: new StorageInfo(new Percentage(0.5), new ByteData(512e9), new ByteData(256e9), new ByteData(256e9)),
            BatteryInfo: new BatteryInfo(new Percentage(0.746), IsInstalled: true, IsCharging: true, IsPluggedIn: true),
            NetworkInfo: new NetworkInfo(true, NetworkInterface.Ethernet, "10.0.0.2", new ByteData(1500), new ByteData(2.5e6))
        ));
        var rows = DashboardRows.Build(metrics, Culture);
        Assert.Equal(
            new[]
            {
                new DashboardSystemRow(
                    SystemMetricKind.CPU,
                    "42.4%",
                    [new(DashboardDetailKind.System, "10.0%"), new(DashboardDetailKind.User, "32.4%"), new(DashboardDetailKind.Idle, "57.6%")],
                    metrics.CPURingBuffer.Values
                ),
                new DashboardSystemRow(
                    SystemMetricKind.Memory,
                    "60.0%",
                    [new(DashboardDetailKind.Used, "9.6 GB"), new(DashboardDetailKind.Available, "6.4 GB"), new(DashboardDetailKind.Total, "16.0 GB")],
                    metrics.MemoryRingBuffer.Values
                ),
                new DashboardSystemRow(
                    SystemMetricKind.Storage,
                    "50.0%",
                    [new(DashboardDetailKind.Used, "256.00 GB"), new(DashboardDetailKind.Available, "256.00 GB"), new(DashboardDetailKind.Total, "512.00 GB")],
                    [],
                    50
                ),
                new DashboardSystemRow(SystemMetricKind.Battery, "74.6%", [new(DashboardDetailKind.Charging)], []),
                new DashboardSystemRow(
                    SystemMetricKind.Network,
                    null,
                    [new(DashboardDetailKind.IPAddress, "10.0.0.2"), new(DashboardDetailKind.Upload, "1.5 kB/s"), new(DashboardDetailKind.Download, "2.5 MB/s")],
                    [],
                    NetworkInterface: NetworkInterface.Ethernet
                ),
            },
            rows
        );
    }

    [Fact]
    public void Build_skips_unmonitored_metrics()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(CPUInfo: new CPUInfo()));
        Assert.Equal(new[] { SystemMetricKind.CPU }, DashboardRows.Build(metrics, Culture).Select(row => row.Kind));
    }

    [Theory]
    [InlineData(true, true, DashboardDetailKind.Charging)]
    [InlineData(false, true, DashboardDetailKind.PluggedIn)]
    [InlineData(false, false, DashboardDetailKind.OnBattery)]
    public void Battery_power_source_detail(bool isCharging, bool isPluggedIn, DashboardDetailKind expected)
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(BatteryInfo: new BatteryInfo(new Percentage(0.5), true, isCharging, isPluggedIn)));
        var row = Assert.Single(DashboardRows.Build(metrics, Culture));
        Assert.Equal(new[] { new DashboardDetail(expected) }, row.Details);
    }

    [Fact]
    public void Missing_battery_and_disconnected_network_have_no_value_or_details()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(BatteryInfo: new BatteryInfo(), NetworkInfo: new NetworkInfo()));
        Assert.Equal(
            new[]
            {
                new DashboardSystemRow(SystemMetricKind.Battery, null, [], []),
                new DashboardSystemRow(SystemMetricKind.Network, null, [], []),
            },
            DashboardRows.Build(metrics, Culture)
        );
    }

    [Fact]
    public void Connection_without_ip_address_keeps_speed_details()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(NetworkInfo: new NetworkInfo(true, NetworkInterface.Cellular, null, new ByteData(10), new ByteData(20))));
        var row = Assert.Single(DashboardRows.Build(metrics, Culture));
        Assert.Equal(new[] { DashboardDetailKind.Upload, DashboardDetailKind.Download }, row.Details.Select(detail => detail.Kind));
        Assert.Equal(NetworkInterface.Cellular, row.NetworkInterface);
    }

    [Fact]
    public void Memory_graph_values_are_clamped_like_cpu()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(MemoryInfo: new MemoryInfo(Percentage: new Percentage(0.001))));
        metrics.Update(new SystemInfoBundle(MemoryInfo: new MemoryInfo(Percentage: new Percentage(1.5))));
        Assert.Equal(new[] { 2d, 100d }, DashboardRows.Build(metrics, Culture)[0].LineGraphValues.TakeLast(2));
    }

    [Theory]
    [InlineData(0.004, 0.4)]
    [InlineData(1.2, 100)]
    [InlineData(-0.1, 0)]
    public void Storage_bar_value_keeps_small_ratios_and_clamps_to_range(double rawValue, double expected)
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(StorageInfo: new StorageInfo(Percentage: new Percentage(rawValue))));
        Assert.Equal(expected, DashboardRows.Build(metrics, Culture)[0].BarGraphValue);
    }

    [Fact]
    public void Line_graph_values_are_clamped_like_mac()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(CPUInfo: new CPUInfo(Percentage: new Percentage(0.0))));
        metrics.Update(new SystemInfoBundle(CPUInfo: new CPUInfo(Percentage: new Percentage(1.2))));
        var values = DashboardRows.Build(metrics, Culture)[0].LineGraphValues;
        Assert.Equal(61, values.Count);
        Assert.Equal(new[] { 2d, 100d }, values.TakeLast(2));
    }
}

public sealed class RunnerCatalogTests
{
    private static readonly Runner Custom = new("custom-id", "Custom", isTemplate: false, FrameOrder.Ascending(2));

    [Fact]
    public void All_lists_built_in_runners_in_mac_order_then_custom_runners()
    {
        Assert.Equal(
            new[] { "duck", "duck-filled", "custom-id" },
            RunnerCatalog.All([Custom]).Select(runner => runner.ID)
        );
    }

    [Theory]
    [InlineData("duck", "duck")]
    [InlineData("duck-filled", "duck-filled")]
    [InlineData("custom-id", "custom-id")]
    [InlineData("deleted-custom-id", "duck")]
    [InlineData("", "duck")]
    public void Resolve_finds_runner_or_falls_back_to_duck(string runnerID, string expectedID)
    {
        Assert.Equal(expectedID, RunnerCatalog.Resolve(runnerID, [Custom]).ID);
    }
}
