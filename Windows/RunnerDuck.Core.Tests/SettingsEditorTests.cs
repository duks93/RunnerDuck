namespace RunnerDuck.Core.Tests;

public sealed class SettingsEditorTests
{
    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("move")]
    public void Failed_custom_metrics_save_restores_order_and_visibility(string operation)
    {
        var first = Source("first");
        var second = Source("second");
        var settings = new AppSettings { RunnerID = "custom-id" };
        settings.CustomMetricsConfiguration.Sources = [first, second];
        settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs = [first.ID];

        Assert.Throws<IOException>(() => SettingsEditor.SaveCustomMetricsChange(settings, current =>
        {
            switch (operation)
            {
                case "add": SettingsEditor.AddCustomMetricsSource(current, Source("third")); break;
                case "remove": SettingsEditor.RemoveCustomMetricsSource(current, first.ID); break;
                case "move": SettingsEditor.MoveCustomMetricsSource(current, 0, 1); break;
            }
        }, _ => throw new IOException("disk full")));

        Assert.Equal(new[] { first, second }, settings.CustomMetricsConfiguration.Sources);
        Assert.Equal(new[] { first.ID }, settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
        Assert.Equal("custom-id", settings.RunnerID);
    }

    [Fact]
    public void Custom_metrics_changes_persist_in_order_and_removal_keeps_original_file()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "metrics.json");
        File.WriteAllText(path, "original JSON");
        var repository = new AppSettingsRepository(Path.Combine(directory.Path, "settings.json"));
        var settings = new AppSettings();
        var first = Source("first") with { FilePath = path };
        var second = Source("second");
        SettingsEditor.SaveCustomMetricsChange(settings, current => SettingsEditor.AddCustomMetricsSource(current, first), repository.Save);
        SettingsEditor.SaveCustomMetricsChange(settings, current => SettingsEditor.AddCustomMetricsSource(current, second), repository.Save);
        SettingsEditor.SaveCustomMetricsChange(settings, current => SettingsEditor.MoveCustomMetricsSource(current, 1, 0), repository.Save);
        Assert.Equal(new[] { second, first }, repository.Load().CustomMetricsConfiguration.Sources);
        SettingsEditor.SetShowsCustomMetricsInMetricsBar(settings, first.ID, true);
        SettingsEditor.SaveCustomMetricsChange(settings, current => SettingsEditor.RemoveCustomMetricsSource(current, first.ID), repository.Save);
        var reloaded = repository.Load();
        Assert.Equal(new[] { second }, reloaded.CustomMetricsConfiguration.Sources);
        Assert.Empty(reloaded.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
        Assert.Equal("original JSON", File.ReadAllText(path));
    }

    private static CustomMetricsSource Source(string name) => new()
    {
        ID = Guid.NewGuid(),
        DisplayName = name,
        FilePath = $@"C:\{name}.json",
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void RemoveCustomMetricsSource_removes_source_and_metrics_bar_visibility()
    {
        var settings = new AppSettings();
        var removed = Source("removed");
        var kept = Source("kept");
        SettingsEditor.AddCustomMetricsSource(settings, removed);
        SettingsEditor.AddCustomMetricsSource(settings, kept);
        SettingsEditor.SetShowsCustomMetricsInMetricsBar(settings, removed.ID, true);
        SettingsEditor.SetShowsCustomMetricsInMetricsBar(settings, kept.ID, true);
        SettingsEditor.RemoveCustomMetricsSource(settings, removed.ID);
        Assert.Equal(new[] { kept }, settings.CustomMetricsConfiguration.Sources);
        Assert.Equal(new[] { kept.ID }, settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
    }

    [Theory]
    [InlineData(0, 2, new[] { "b", "c", "a" })]
    [InlineData(2, 0, new[] { "c", "a", "b" })]
    [InlineData(1, 1, new[] { "a", "b", "c" })]
    [InlineData(0, 3, new[] { "a", "b", "c" })]
    [InlineData(-1, 0, new[] { "a", "b", "c" })]
    public void MoveCustomMetricsSource_reorders_sources(int fromIndex, int toIndex, string[] expected)
    {
        var settings = new AppSettings();
        foreach (var name in new[] { "a", "b", "c" })
        {
            SettingsEditor.AddCustomMetricsSource(settings, Source(name));
        }
        SettingsEditor.MoveCustomMetricsSource(settings, fromIndex, toIndex);
        Assert.Equal(expected, settings.CustomMetricsConfiguration.Sources.Select(source => source.DisplayName));
    }

    [Theory]
    [InlineData(SystemMetricKind.Memory)]
    [InlineData(SystemMetricKind.Storage)]
    [InlineData(SystemMetricKind.Battery)]
    [InlineData(SystemMetricKind.Network)]
    public void SetShowsInMetricsBar_turns_on_monitoring_when_needed(SystemMetricKind kind)
    {
        var settings = new AppSettings();
        SettingsEditor.SetMonitors(settings, kind, false);
        Assert.True(SettingsEditor.SetShowsInMetricsBar(settings, kind, true));
        Assert.True(SettingsEditor.ShowsInMetricsBar(settings, kind));
        Assert.True(SettingsEditor.IsMonitored(settings, kind));
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, kind, true));
    }

    [Theory]
    [InlineData(SystemMetricKind.Memory)]
    [InlineData(SystemMetricKind.Network)]
    public void SetShowsInMetricsBar_off_keeps_monitoring_unchanged(SystemMetricKind kind)
    {
        var settings = new AppSettings();
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, kind, true));
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, kind, false));
        Assert.False(SettingsEditor.ShowsInMetricsBar(settings, kind));
        Assert.True(SettingsEditor.IsMonitored(settings, kind));
    }

    [Theory]
    [InlineData(SystemMetricKind.Memory)]
    [InlineData(SystemMetricKind.Storage)]
    [InlineData(SystemMetricKind.Battery)]
    [InlineData(SystemMetricKind.Network)]
    public void SetShowsInMetricsBar_activates_only_the_requested_metric(SystemMetricKind kind)
    {
        var settings = new AppSettings();
        var monitoredKinds = new[] { SystemMetricKind.Memory, SystemMetricKind.Storage, SystemMetricKind.Battery, SystemMetricKind.Network };
        foreach (var monitoredKind in monitoredKinds)
        {
            SettingsEditor.SetMonitors(settings, monitoredKind, false);
        }
        Assert.True(SettingsEditor.SetShowsInMetricsBar(settings, kind, true));
        Assert.Equal(
            monitoredKinds.Select(monitoredKind => monitoredKind == kind),
            monitoredKinds.Select(monitoredKind => SettingsEditor.IsMonitored(settings, monitoredKind))
        );
        Assert.Equal(
            monitoredKinds.Select(monitoredKind => monitoredKind == kind),
            monitoredKinds.Select(monitoredKind => SettingsEditor.ShowsInMetricsBar(settings, monitoredKind))
        );
    }

    [Fact]
    public void SetShowsInMetricsBar_off_for_hidden_item_keeps_monitoring()
    {
        var settings = new AppSettings();
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, SystemMetricKind.Storage, false));
        Assert.False(SettingsEditor.ShowsInMetricsBar(settings, SystemMetricKind.Storage));
        Assert.True(SettingsEditor.IsMonitored(settings, SystemMetricKind.Storage));
    }

    [Fact]
    public void SetShowsInMetricsBar_cpu_never_requires_activation()
    {
        var settings = new AppSettings();
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, SystemMetricKind.CPU, false));
        Assert.False(settings.MetricsBarConfiguration.ShowsCPU);
        Assert.False(SettingsEditor.SetShowsInMetricsBar(settings, SystemMetricKind.CPU, true));
        Assert.True(settings.MetricsBarConfiguration.ShowsCPU);
    }

    [Fact]
    public void SetMonitors_changes_only_the_requested_metric()
    {
        var settings = new AppSettings();
        SettingsEditor.SetMonitors(settings, SystemMetricKind.Storage, false);
        var configuration = settings.SystemMetricsConfiguration;
        Assert.Equal(
            new[] { true, false, true, true },
            new[] { configuration.MonitorsMemory, configuration.MonitorsStorage, configuration.MonitorsBattery, configuration.MonitorsNetwork }
        );
    }

    [Fact]
    public void SetShowsCustomMetricsInMetricsBar_adds_and_removes_visibility()
    {
        var settings = new AppSettings();
        var id = Guid.NewGuid();
        SettingsEditor.SetShowsCustomMetricsInMetricsBar(settings, id, true);
        Assert.Contains(id, settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
        SettingsEditor.SetShowsCustomMetricsInMetricsBar(settings, id, false);
        Assert.Empty(settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
    }
}
