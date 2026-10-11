using System.Globalization;

namespace RunnerDuck.Core.Tests;

public sealed class MetricsTests
{
    [Fact]
    public void RingBuffer_starts_with_61_values_of_two()
    {
        var buffer = new RingBuffer();
        Assert.Equal(61, buffer.Values.Count);
        Assert.All(buffer.Values, value => Assert.Equal(2d, value));
    }

    [Fact]
    public void RingBuffer_append_keeps_length_and_drops_oldest_value()
    {
        var buffer = new RingBuffer(length: 3);
        buffer.Append(10);
        buffer.Append([20, 30, 40]);
        Assert.Equal(new[] { 20d, 30d, 40d }, buffer.Values);
    }

    [Fact]
    public void Metrics_update_appends_cpu_and_memory_percentages()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(
            CPUInfo: new CPUInfo(Percentage: new Percentage(0.42)),
            MemoryInfo: new MemoryInfo(Percentage: new Percentage(0.5))
        ));
        Assert.Equal(42d, metrics.CPURingBuffer.Values[^1]);
        Assert.Equal(50d, metrics.MemoryRingBuffer.Values[^1]);
    }

    [Fact]
    public void Metrics_update_stores_latest_bundle_with_all_fields()
    {
        var metrics = new Metrics();
        var bundle = new SystemInfoBundle(
            CPUInfo: new CPUInfo(new Percentage(0.3), new Percentage(0.1), new Percentage(0.2), new Percentage(0.7)),
            MemoryInfo: new MemoryInfo(Percentage: new Percentage(0.6), Used: new ByteData(9.6e9), Available: new ByteData(6.4e9), Total: new ByteData(16e9)),
            StorageInfo: new StorageInfo(new Percentage(0.5), new ByteData(512e9), new ByteData(256e9), new ByteData(256e9)),
            BatteryInfo: new BatteryInfo(Percentage: new Percentage(0.8), IsInstalled: true, IsCharging: true, IsPluggedIn: true),
            NetworkInfo: new NetworkInfo(HasConnection: true, NetworkInterface: NetworkInterface.WiFi, Upload: new ByteData(10), Download: new ByteData(20))
        );
        metrics.Update(bundle);
        Assert.Same(bundle, metrics.SystemInfoBundle);
        Assert.Equal(new Percentage(0.2), metrics.SystemInfoBundle.CPUInfo!.User);
        Assert.Equal(new ByteData(256e9), metrics.SystemInfoBundle.StorageInfo!.Available);
        Assert.True(metrics.SystemInfoBundle.BatteryInfo!.IsCharging);
        Assert.Equal(new ByteData(6.4e9), metrics.SystemInfoBundle.MemoryInfo!.Available);
        Assert.Equal(NetworkInterface.WiFi, metrics.SystemInfoBundle.NetworkInfo!.NetworkInterface);
    }

    [Fact]
    public void Metrics_consecutive_updates_accumulate_history()
    {
        var metrics = new Metrics();
        foreach (var rawValue in new[] { 0.1, 0.2, 0.3 })
        {
            metrics.Update(new SystemInfoBundle(
                CPUInfo: new CPUInfo(Percentage: new Percentage(rawValue)),
                MemoryInfo: new MemoryInfo(Percentage: new Percentage(rawValue + 0.5))
            ));
        }
        Assert.Equal(new[] { 2d, 10d, 20d, 30d }, metrics.CPURingBuffer.Values.TakeLast(4));
        Assert.Equal(new[] { 2d, 60d, 70d, 80d }, metrics.MemoryRingBuffer.Values.TakeLast(4));
    }

    [Fact]
    public void Metrics_update_without_memory_keeps_existing_memory_history()
    {
        var metrics = new Metrics();
        metrics.Update(new SystemInfoBundle(
            CPUInfo: new CPUInfo(Percentage: new Percentage(0.4)),
            MemoryInfo: new MemoryInfo(Percentage: new Percentage(0.9))
        ));
        var memoryHistory = metrics.MemoryRingBuffer.Values;
        metrics.Update(new SystemInfoBundle(CPUInfo: new CPUInfo(Percentage: new Percentage(0.1))));
        Assert.Equal(10d, metrics.CPURingBuffer.Values[^1]);
        Assert.Equal(memoryHistory, metrics.MemoryRingBuffer.Values);
        Assert.Null(metrics.SystemInfoBundle.MemoryInfo);
    }

    [Theory]
    [InlineData(0.4236, 42.4)]
    [InlineData(0.00049, 0.0)]
    [InlineData(0.0005, 0.1)]
    [InlineData(0.00051, 0.1)]
    [InlineData(1.0, 100.0)]
    public void Percentage_value_rounds_half_away_from_zero_to_one_decimal(double rawValue, double expected)
    {
        Assert.Equal(expected, new Percentage(rawValue).Value);
    }

    [Theory]
    [InlineData(0, 0, "B")]
    [InlineData(999, 999, "B")]
    [InlineData(1000, 1, "kB")]
    [InlineData(1500, 1.5, "kB")]
    [InlineData(1_000_000, 1, "MB")]
    [InlineData(1_234_567_890, 1.235, "GB")]
    public void ByteData_uses_decimal_units(double byteCount, double value, string unit)
    {
        Assert.Equal((value, unit), new ByteData(byteCount).ReadableValue);
    }

    [Fact]
    public void MetricsFormatter_drops_decimals_at_one_hundred_percent_like_mac_menu_bar()
    {
        Assert.Equal(" 100%", MetricsFormatter.FormatPercentage(new Percentage(1.0), CultureInfo.InvariantCulture));
        Assert.Equal("99.9%", MetricsFormatter.FormatPercentage(new Percentage(0.999), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MetricsFormatter_respects_format_provider()
    {
        Assert.Equal("42,4%", MetricsFormatter.FormatPercentage(new Percentage(0.424), CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("42.4%", MetricsFormatter.FormatPercentage(new Percentage(0.424), CultureInfo.GetCultureInfo("ko-KR")));
    }

    [Fact]
    public void MetricsFormatter_formats_values_with_fixed_widths()
    {
        var culture = CultureInfo.InvariantCulture;
        Assert.Equal("42.4%", MetricsFormatter.FormatPercentage(new Percentage(0.424), culture));
        Assert.Equal(" 5.0%", MetricsFormatter.FormatPercentage(new Percentage(0.05), culture));
        Assert.Equal("99.5 kB/s", MetricsFormatter.FormatSpeed(new ByteData(99_500), culture));
        Assert.Equal(" 100 kB/s", MetricsFormatter.FormatSpeed(new ByteData(100_000), culture));
        Assert.Equal(" 1.5 kB", MetricsFormatter.FormatBytes(new ByteData(1500), culture));
        Assert.Equal("512.00 GB", MetricsFormatter.FormatStorage(new ByteData(512e9), culture));
        Assert.Equal(" 2.5 MB/s", MetricsFormatter.FormatSpeed(new ByteData(2.5e6), culture));
        Assert.Equal(" 123 kB/s", MetricsFormatter.FormatSpeed(new ByteData(123_456), culture));
    }
}
