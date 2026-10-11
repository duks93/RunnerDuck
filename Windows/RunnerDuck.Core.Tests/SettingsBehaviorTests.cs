namespace RunnerDuck.Core.Tests;

public sealed class SettingsMonitoringTests
{
    [Theory]
    [InlineData(SystemMetricKind.Memory)]
    [InlineData(SystemMetricKind.Storage)]
    [InlineData(SystemMetricKind.Battery)]
    [InlineData(SystemMetricKind.Network)]
    public void SetMonitoring_off_also_hides_the_metric_from_metrics_bar(SystemMetricKind kind)
    {
        var settings = new AppSettings();
        SettingsEditor.SetShowsInMetricsBar(settings, kind, true);
        SettingsEditor.SetMonitoring(settings, kind, false);
        Assert.False(SettingsEditor.IsMonitored(settings, kind));
        Assert.False(SettingsEditor.ShowsInMetricsBar(settings, kind));
    }

    [Theory]
    [InlineData(SystemMetricKind.Memory)]
    [InlineData(SystemMetricKind.Network)]
    public void SetMonitoring_on_does_not_show_the_metric_in_metrics_bar(SystemMetricKind kind)
    {
        var settings = new AppSettings();
        SettingsEditor.SetMonitoring(settings, kind, false);
        SettingsEditor.SetMonitoring(settings, kind, true);
        Assert.True(SettingsEditor.IsMonitored(settings, kind));
        Assert.False(SettingsEditor.ShowsInMetricsBar(settings, kind));
    }

    [Fact]
    public void SetMonitoring_ignores_cpu_like_mac()
    {
        var settings = new AppSettings();
        SettingsEditor.SetMonitoring(settings, SystemMetricKind.CPU, false);
        Assert.True(SettingsEditor.IsMonitored(settings, SystemMetricKind.CPU));
        Assert.True(settings.MetricsBarConfiguration.ShowsCPU);
    }

    [Fact]
    public void SetMonitoring_off_keeps_other_metrics_unchanged()
    {
        var settings = new AppSettings();
        SettingsEditor.SetShowsInMetricsBar(settings, SystemMetricKind.Memory, true);
        SettingsEditor.SetShowsInMetricsBar(settings, SystemMetricKind.Storage, true);
        SettingsEditor.SetMonitoring(settings, SystemMetricKind.Memory, false);
        Assert.True(SettingsEditor.IsMonitored(settings, SystemMetricKind.Storage));
        Assert.True(SettingsEditor.ShowsInMetricsBar(settings, SystemMetricKind.Storage));
        Assert.True(settings.MetricsBarConfiguration.ShowsCPU);
    }
}

public sealed class LaunchAtLoginStateTests
{
    [Theory]
    [InlineData(LaunchAtLoginState.Enabled, true, true, LaunchAtLoginNote.None)]
    [InlineData(LaunchAtLoginState.Disabled, false, true, LaunchAtLoginNote.None)]
    [InlineData(LaunchAtLoginState.DisabledByUser, false, false, LaunchAtLoginNote.TurnOnInTaskManager)]
    [InlineData(LaunchAtLoginState.DisabledByPolicy, false, false, LaunchAtLoginNote.ManagedByPolicy)]
    [InlineData(LaunchAtLoginState.EnabledByPolicy, true, false, LaunchAtLoginNote.ManagedByPolicy)]
    [InlineData(LaunchAtLoginState.Unavailable, false, false, LaunchAtLoginNote.Unavailable)]
    public void State_maps_to_toggle_and_note(LaunchAtLoginState state, bool isOn, bool canChange, LaunchAtLoginNote note)
    {
        Assert.Equal(isOn, state.IsOn());
        Assert.Equal(canChange, state.CanChange());
        Assert.Equal(note, state.Note());
    }
}
