using System.Net;
using System.Text;
using RunnerDuck.AIUsage;

namespace RunnerDuck.Core.Tests;

public sealed class AIUsageMonitorTests
{
    private static readonly AIUsageBundle Codex = new(AIUsageProvider.Codex, AIUsageStatus.Available, [], DateTimeOffset.UnixEpoch);
    private static TaskCompletionSource<IReadOnlyList<AIUsageBundle>> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void Save(string path, bool enabled)
    {
        var settings = new AppSettings();
        settings.AIUsageConfiguration.Set(AIUsageSource.Codex, enabled);
        new AppSettingsRepository(path).Save(settings);
    }

    [Fact]
    public async Task Saved_opt_out_reaches_service_and_sends_no_provider_request()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, false);
        using var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var service = new AIUsageService(new AIUsageEnvironment
        {
            HomeDirectory = directory.Path,
            ReadFile = name => name.Replace('\\', '/').EndsWith(".codex/auth.json", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes("""{"tokens":{"access_token":"test-token"}}""") : null,
            PathExists = _ => false,
            Http = http,
            Now = () => DateTimeOffset.UnixEpoch,
        });
        using var monitor = new AIUsageMonitor(path, service.RefreshAsync, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        await monitor.RefreshAsync();
        Assert.Empty(monitor.Snapshot.Bundles!);
        Assert.Equal(0, handler.RequestCount);
        Save(path, true);
        await monitor.RefreshAsync();
        Assert.Single(monitor.Snapshot.Bundles!);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Missing_settings_directory_uses_default_detection_and_sends_a_provider_request()
    {
        using var directory = new TestDirectory();
        var path = directory.File(Path.Combine("not-created", "settings.json"));
        using var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var service = new AIUsageService(new AIUsageEnvironment
        {
            HomeDirectory = directory.Path,
            ReadFile = name => name.Replace('\\', '/').EndsWith(".codex/auth.json", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes("""{"tokens":{"access_token":"test-token"}}""") : null,
            PathExists = _ => false,
            Http = http,
            Now = () => DateTimeOffset.UnixEpoch,
        });
        using var monitor = new AIUsageMonitor(path, service.RefreshAsync, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        await monitor.RefreshAsync();
        Assert.Single(monitor.Snapshot.Bundles!);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task First_refresh_keeps_loading_state_until_the_response_arrives()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var pending = Pending();
        using var monitor = new AIUsageMonitor(path, (_, _) => pending.Task,
            settingsCheckInterval: Timeout.InfiniteTimeSpan);
        var request = monitor.RefreshAsync();
        Assert.Null(monitor.Snapshot.Bundles);
        Assert.Null(monitor.Snapshot.LastRefreshDate);
        pending.SetResult([Codex]);
        await request;
        Assert.Equal(new[] { Codex }, monitor.Snapshot.Bundles);
        Assert.NotNull(monitor.Snapshot.LastRefreshDate);
    }

    [Fact]
    public async Task Settings_change_cancels_previous_request_and_discards_its_late_result()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var pending = Pending();
        CancellationToken oldToken = default;
        using var monitor = new AIUsageMonitor(path, (configuration, token) =>
        {
            if (!configuration.IsEnabled(AIUsageSource.Codex, true)) return Task.FromResult<IReadOnlyList<AIUsageBundle>>([]);
            oldToken = token;
            return pending.Task;
        }, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        var oldRefresh = monitor.RefreshAsync();
        Save(path, false);
        await monitor.ReloadConfigurationAsync();
        Assert.True(oldToken.IsCancellationRequested);
        Assert.Empty(monitor.Snapshot.Bundles!);
        pending.SetResult([Codex]);
        await oldRefresh;
        Assert.Empty(monitor.Snapshot.Bundles!);
    }

    [Fact]
    public async Task Disabled_rows_disappear_before_replacement_refresh_finishes()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var pooled = Codex with { AccountID = "openCodex/other" };
        var pending = Pending();
        using var monitor = new AIUsageMonitor(path, (configuration, _) => configuration.IsEnabled(AIUsageSource.Codex, true)
            ? Task.FromResult<IReadOnlyList<AIUsageBundle>>([Codex, pooled]) : pending.Task,
            settingsCheckInterval: Timeout.InfiniteTimeSpan);
        await monitor.RefreshAsync();
        Save(path, false);
        var replacement = monitor.ReloadConfigurationAsync();
        Assert.Equal(new[] { pooled }, monitor.Snapshot.Bundles);
        pending.SetResult([pooled]);
        await replacement;
    }

    [Fact]
    public async Task Result_rechecks_saved_settings_even_before_the_observer_runs()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var pending = Pending();
        using var monitor = new AIUsageMonitor(path, (configuration, _) => configuration.IsEnabled(AIUsageSource.Codex, true)
            ? pending.Task : Task.FromResult<IReadOnlyList<AIUsageBundle>>([]),
            settingsCheckInterval: Timeout.InfiniteTimeSpan);
        var request = monitor.RefreshAsync();
        Save(path, false);
        pending.SetResult([Codex]);
        await request;
        Assert.Empty(monitor.Snapshot.Bundles!);
    }

    [Fact]
    public async Task Unrelated_settings_change_does_not_issue_another_usage_request()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var count = 0;
        using var monitor = new AIUsageMonitor(path, (_, _) =>
        {
            count++;
            return Task.FromResult<IReadOnlyList<AIUsageBundle>>([Codex]);
        }, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        await monitor.RefreshAsync();
        var repository = new AppSettingsRepository(path);
        var settings = repository.Load();
        settings.ShowsMetricsBar = true;
        repository.Save(settings);
        await monitor.ReloadConfigurationAsync();
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"aiUsageConfiguration":null}""")]
    [InlineData("""{"aiUsageConfiguration":{"overrides":null}}""")]
    public async Task Unreadable_configuration_never_falls_back_to_enabled_sources(string json)
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        File.WriteAllText(path, json);
        var count = 0;
        using var monitor = new AIUsageMonitor(path, (_, _) =>
        {
            count++;
            return Task.FromResult<IReadOnlyList<AIUsageBundle>>([Codex]);
        }, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        await monitor.RefreshAsync();
        Assert.Equal(0, count);
        Assert.Empty(monitor.Snapshot.Bundles!);
        Save(path, true);
        await monitor.ReloadConfigurationAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Observer_detects_an_atomic_settings_update_without_a_widget_refresh()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        using var monitor = new AIUsageMonitor(path, (configuration, _) => Task.FromResult<IReadOnlyList<AIUsageBundle>>(
            configuration.IsEnabled(AIUsageSource.Codex, true) ? [Codex] : []),
            settingsCheckInterval: TimeSpan.FromMilliseconds(20));
        var disabled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await monitor.RefreshAsync();
        monitor.Updated += () =>
        {
            if (monitor.Snapshot.Bundles is { Count: 0 }) disabled.TrySetResult();
        };
        Save(path, false);
        await disabled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(monitor.Snapshot.Bundles!);
    }

    [Fact]
    public async Task Dispose_cancels_and_prevents_late_publication()
    {
        using var directory = new TestDirectory();
        var path = directory.File("settings.json");
        Save(path, true);
        var pending = Pending();
        CancellationToken token = default;
        using var monitor = new AIUsageMonitor(path, (_, cancellation) =>
        {
            token = cancellation;
            return pending.Task;
        }, settingsCheckInterval: Timeout.InfiniteTimeSpan);
        var request = monitor.RefreshAsync();
        monitor.Dispose();
        Assert.True(token.IsCancellationRequested);
        pending.SetResult([Codex]);
        await request;
        Assert.Null(monitor.Snapshot.Bundles);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}
