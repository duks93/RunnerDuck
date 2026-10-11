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
