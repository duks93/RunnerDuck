using System.Runtime.ExceptionServices;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tests;

public sealed class MetricsTrayIconsTests
{
    [Fact]
    public void Selection_changes_add_and_remove_the_actual_icon_resources() => OnSTA(() =>
    {
        using var menu = new ContextMenuStrip();
        using var icons = new MetricsTrayIcons(menu, () => { }, item => item.Kind.ToString(), visible: false);
        var configuration = new MetricsBarConfiguration { ShowsMemory = true };
        var readings = new SystemInfoBundle(CPUInfo: new(new Percentage(0.42)), MemoryInfo: new(new Percentage(0.65)));
        icons.Update(MetricsBarItems.Build(configuration, readings, []), 16, Color.Black);
        Assert.Equal(new[] { "CPU", "Memory" }, icons.Entries.Keys);
        var removed = icons.Entries["Memory"];
        configuration.ShowsMemory = false;
        icons.Update(MetricsBarItems.Build(configuration, readings, []), 16, Color.Black);
        Assert.Single(icons.Entries);
        Assert.Null(removed.Icon);
        var cpu = icons.Entries["CPU"];
        icons.Update([], 16, Color.Black);
        Assert.Empty(icons.Entries);
        Assert.Null(cpu.Icon);
    });

    [Fact]
    public void Production_metric_keys_reuse_all_five_reserved_entries() => OnSTA(() =>
    {
        using var menu = new ContextMenuStrip();
        using var icons = new MetricsTrayIcons(menu, () => { }, item => item.Kind.ToString(), visible: false);
        var reserved = icons.CachedEntries.ToDictionary(pair => pair.Key, pair => pair.Value);
        string[] expectedOrder = ["CPU", "Memory", "Storage", "Battery", "Network"];
        var configuration = new MetricsBarConfiguration
        {
            ShowsCPU = true, ShowsMemory = true, ShowsStorage = true, ShowsBattery = true, ShowsNetwork = true,
        };
        var network = new NetworkInfo(Upload: new ByteData(1500), Download: new ByteData(2_500_000));
        var earlyReadings = MetricsBarItems.Build(configuration, new SystemInfoBundle(NetworkInfo: network), []);
        Assert.Equal("Network", Assert.Single(earlyReadings).Key);
        icons.Update(earlyReadings, 16, Color.Black);
        Assert.Same(reserved["Network"], icons.Entries["Network"]);
        var allReadings = MetricsBarItems.Build(configuration, new SystemInfoBundle(
            CPUInfo: new CPUInfo(new Percentage(0.42)),
            MemoryInfo: new MemoryInfo(new Percentage(0.6)),
            StorageInfo: new StorageInfo(new Percentage(0.8)),
            BatteryInfo: new BatteryInfo(new Percentage(0.75), IsInstalled: true),
            NetworkInfo: network), []);
        Assert.Equal(expectedOrder, allReadings.Select(item => item.Key));
        icons.Update(allReadings, 16, Color.Black);
        Assert.Equal(5, icons.CachedEntries.Count);
        Assert.Equal(expectedOrder, icons.CachedEntries.Keys);
        foreach (var key in expectedOrder) Assert.Same(reserved[key], icons.Entries[key]);
        icons.Update(MetricsBarItems.Build(configuration, new SystemInfoBundle(BatteryInfo: new BatteryInfo()), []), 16, Color.Black);
        Assert.Same(reserved["Battery"], Assert.Single(icons.Entries).Value);
        Assert.Equal(expectedOrder, icons.CachedEntries.Keys);
    });

    [Fact]
    public void Missing_readings_and_toggles_reuse_the_same_notification_entry() => OnSTA(() =>
    {
        using var menu = new ContextMenuStrip();
        using var icons = new MetricsTrayIcons(menu, () => { }, _ => "CPU", visible: false);
        Assert.Equal(new[] { "CPU", "Memory", "Storage", "Battery", "Network" }, icons.CachedEntries.Keys);
        Assert.All(icons.CachedEntries.Values, entry => Assert.Null(entry.Icon));
        var cpu = icons.CachedEntries["CPU"];
        var custom = new MetricsBarItem("CustomMetrics:test", MetricsBarItemKind.CustomMetrics, "9", "9");
        var reading = new MetricsBarItem("CPU", MetricsBarItemKind.CPU, "42", "42.0%");
        icons.Update([custom, reading], 16, Color.Black);
        var customEntry = icons.Entries[custom.Key];
        Assert.Same(cpu, icons.Entries["CPU"]);
        icons.Update([], 16, Color.Black);
        Assert.Empty(icons.Entries);
        Assert.Null(cpu.Icon);
        Assert.Null(customEntry.Icon);
        icons.Update([reading, custom], 32, Color.White);
        Assert.Same(cpu, icons.Entries["CPU"]);
        Assert.Same(customEntry, icons.Entries[custom.Key]);
        Assert.NotNull(cpu.Icon);
        Assert.Equal(6, icons.CachedEntries.Count);
        icons.Dispose();
        Assert.Empty(icons.CachedEntries);
        Assert.Null(customEntry.Icon);
    });

    [Fact]
    public void Theme_dpi_and_value_changes_rebuild_but_unchanged_pixels_reuse_the_icon() => OnSTA(() =>
    {
        using var menu = new ContextMenuStrip();
        using var icons = new MetricsTrayIcons(menu, () => { }, _ => "CPU", visible: false);
        var item = new MetricsBarItem("CPU", MetricsBarItemKind.CPU, "42", "42.0%");
        icons.Update([item], 16, Color.Black);
        var original = icons.Entries["CPU"].Icon;
        icons.Update([item with { DetailText = "42.1%" }], 16, Color.Black);
        Assert.Same(original, icons.Entries["CPU"].Icon);
        Assert.Equal("CPU: 42.1%", icons.Entries["CPU"].ToolTip);
        icons.Update([item], 16, Color.White);
        Assert.NotSame(original, icons.Entries["CPU"].Icon);
        var dark = icons.Entries["CPU"].Icon;
        icons.Update([item], 32, Color.White);
        Assert.NotSame(dark, icons.Entries["CPU"].Icon);
        Assert.Equal(32, icons.Entries["CPU"].Icon!.Width);
        var resized = icons.Entries["CPU"].Icon;
        icons.Update([item with { IconText = "99" }], 32, Color.White);
        Assert.NotSame(resized, icons.Entries["CPU"].Icon);
    });

    [Theory]
    [InlineData("100", 16)]
    [InlineData("2.5M", 16)]
    [InlineData("---", 24)]
    [InlineData("42", 32)]
    public void Renderer_produces_visible_text_on_a_transparent_background(string text, int size) => OnSTA(() =>
    {
        using var bitmap = MetricsTrayIcons.Entry.Render(text, size, Color.White);
        var pixels = Enumerable.Range(0, size).SelectMany(y => Enumerable.Range(0, size).Select(x => bitmap.GetPixel(x, y))).ToList();
        Assert.Contains(pixels, pixel => pixel.A == 0);
        Assert.Contains(pixels, pixel => pixel.A > 0 && pixel.R > 200);
    });

    [Fact]
    public void Custom_metric_titles_fit_tooltip_limits_and_are_removed_on_dispose() => OnSTA(() =>
    {
        using var menu = new ContextMenuStrip();
        using var icons = new MetricsTrayIcons(menu, () => { }, item => item.Title!, visible: false);
        icons.Update([new MetricsBarItem("custom", MetricsBarItemKind.CustomMetrics, "9", "9", Title: new string('x', 200))], 16, Color.Black);
        var entry = icons.Entries["custom"];
        Assert.Equal(127, entry.ToolTip.Length);
        icons.Dispose();
        Assert.Null(entry.Icon);
        Assert.Empty(icons.Entries);
    });

    private static void OnSTA(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
