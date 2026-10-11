using System.Globalization;

namespace RunnerDuck.Core.Tests;

public sealed class MetricsBarItemsTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static readonly SystemInfoBundle FullBundle = new(
        CPUInfo: new CPUInfo(Percentage: new Percentage(0.424)),
        MemoryInfo: new MemoryInfo(Percentage: new Percentage(0.6)),
        StorageInfo: new StorageInfo(Percentage: new Percentage(1.0)),
        BatteryInfo: new BatteryInfo(Percentage: new Percentage(0.746), IsInstalled: true),
        NetworkInfo: new NetworkInfo(Upload: new ByteData(1500), Download: new ByteData(2_500_000))
    );

    private static CustomMetricsBundle Bundle(Guid id, string? metricsBarValue, bool isFailed = false) => new(
        id,
        new CustomMetricsSnapshot { Title = $"Title {metricsBarValue}", MetricsBarValue = metricsBarValue, Metrics = [], LastUpdatedDate = DateTimeOffset.UnixEpoch },
        isFailed
    );

    [Fact]
    public void Default_configuration_shows_only_cpu()
    {
        var item = Assert.Single(MetricsBarItems.Build(new MetricsBarConfiguration(), FullBundle, [], Culture));
        Assert.Equal(new MetricsBarItem("CPU", MetricsBarItemKind.CPU, "42", "42.4%"), item);
    }

    [Fact]
    public void All_system_items_follow_mac_order()
    {
        var configuration = new MetricsBarConfiguration { ShowsMemory = true, ShowsStorage = true, ShowsBattery = true, ShowsNetwork = true };
        var items = MetricsBarItems.Build(configuration, FullBundle, [], Culture);
        Assert.Equal(
            new[]
            {
                new MetricsBarItem("CPU", MetricsBarItemKind.CPU, "42", "42.4%"),
                new MetricsBarItem("Memory", MetricsBarItemKind.Memory, "60", "60.0%"),
                new MetricsBarItem("Storage", MetricsBarItemKind.Storage, "100", "100%"),
                new MetricsBarItem("Battery", MetricsBarItemKind.Battery, "75", "74.6%"),
                new MetricsBarItem("Network", MetricsBarItemKind.Network, "2.5M", "⬆ 1.5 kB/s ⬇ 2.5 MB/s"),
            },
            items
        );
    }

    [Fact]
    public void Items_without_collected_values_are_skipped()
    {
        var configuration = new MetricsBarConfiguration { ShowsMemory = true, ShowsNetwork = true };
        var items = MetricsBarItems.Build(configuration, new SystemInfoBundle(CPUInfo: new CPUInfo()), [], Culture);
        Assert.Equal(new[] { MetricsBarItemKind.CPU }, items.Select(item => item.Kind));
    }

    [Fact]
    public void Missing_battery_has_placeholder_without_value()
    {
        var configuration = new MetricsBarConfiguration { ShowsCPU = false, ShowsBattery = true };
        var item = Assert.Single(MetricsBarItems.Build(configuration, new SystemInfoBundle(BatteryInfo: new BatteryInfo()), [], Culture));
        Assert.Equal(new MetricsBarItem("Battery", MetricsBarItemKind.Battery, "---", "", HasValue: false), item);
    }

    [Fact]
    public void Custom_metrics_show_only_visible_sources_with_truncated_icon_text()
    {
        var visibleID = Guid.NewGuid();
        var failedID = Guid.NewGuid();
        var hiddenID = Guid.NewGuid();
        var configuration = new MetricsBarConfiguration { ShowsCPU = false, VisibleCustomMetricsSourceIDs = [visibleID, failedID] };
        var items = MetricsBarItems.Build(
            configuration,
            FullBundle,
            [Bundle(visibleID, "$123.45"), Bundle(hiddenID, "9"), Bundle(failedID, "7", isFailed: true)],
            Culture
        );
        Assert.Equal(
            new[]
            {
                new MetricsBarItem($"CustomMetrics:{visibleID}", MetricsBarItemKind.CustomMetrics, "$123", "$123.45", true, visibleID, "Title $123.45"),
                new MetricsBarItem($"CustomMetrics:{failedID}", MetricsBarItemKind.CustomMetrics, "---", "---", false, failedID, "Title 7"),
            },
            items
        );
    }

    [Fact]
    public void Custom_icon_text_truncation_keeps_whole_text_elements()
    {
        var id = Guid.NewGuid();
        var configuration = new MetricsBarConfiguration { ShowsCPU = false, VisibleCustomMetricsSourceIDs = [id] };
        var item = Assert.Single(MetricsBarItems.Build(configuration, FullBundle, [Bundle(id, "🐈🐈🐈🐈🐈")], Culture));
        Assert.Equal("🐈🐈🐈🐈", item.IconText);
    }

    [Theory]
    [InlineData(0, "0B")]
    [InlineData(999, "999B")]
    [InlineData(1500, "1.5K")]
    [InlineData(12_345, "12K")]
    [InlineData(345_000, "345K")]
    [InlineData(2_500_000, "2.5M")]
    [InlineData(1_200_000_000, "1.2G")]
    [InlineData(9_940, "9.9K")]
    [InlineData(9_960, "10K")]
    [InlineData(999_400, "999K")]
    [InlineData(999_600, "1.0M")]
    [InlineData(999_999, "1.0M")]
    [InlineData(999_999_999, "1.0G")]
    public void FormatCompactSpeed_fits_tray_icon(double bytesPerSecond, string expected)
    {
        var text = MetricsFormatter.FormatCompactSpeed(new ByteData(bytesPerSecond), Culture);
        Assert.Equal(expected, text);
        Assert.True(text.Length <= MetricsBarItems.MaximumIconTextLength);
    }

    [Fact]
    public void Network_item_keeps_full_speed_in_detail_text()
    {
        var configuration = new MetricsBarConfiguration { ShowsCPU = false, ShowsNetwork = true };
        var bundle = new SystemInfoBundle(NetworkInfo: new NetworkInfo(Upload: new ByteData(999_999), Download: new ByteData(999_999)));
        var item = Assert.Single(MetricsBarItems.Build(configuration, bundle, [], Culture));
        Assert.Equal("1.0M", item.IconText);
        Assert.Equal("⬆1000 kB/s ⬇1000 kB/s", item.DetailText);
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.425, "43")]
    [InlineData(0.995, "100")]
    [InlineData(1.0, "100")]
    public void FormatCompactPercentage_rounds_to_integer(double rawValue, string expected)
    {
        Assert.Equal(expected, MetricsFormatter.FormatCompactPercentage(new Percentage(rawValue), Culture));
    }
}
