// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using RunnerDuck.AIUsage;

namespace RunnerDuck.Core;

public sealed record AIUsageMonitorSnapshot(IReadOnlyList<AIUsageBundle>? Bundles, DateTimeOffset? LastRefreshDate);

public sealed class AIUsageMonitor : IDisposable
{
    private readonly AppSettingsRepository repository;
    private readonly Func<AIUsageConfiguration, CancellationToken, Task<IReadOnlyList<AIUsageBundle>>> refresh;
    private readonly Action<string> log;
    private readonly Lock gate = new();
    private readonly Timer settingsTimer;
    private AIUsageConfiguration? configuration;
    private CancellationTokenSource? currentRequest;
    private AIUsageMonitorSnapshot snapshot = new(null, null);
    private bool disposed;

    public AIUsageMonitor(
        string settingsFile,
        Func<AIUsageConfiguration, CancellationToken, Task<IReadOnlyList<AIUsageBundle>>> refresh,
        Action<string>? log = null,
        TimeSpan? settingsCheckInterval = null)
    {
        repository = new(settingsFile);
        this.refresh = refresh;
        this.log = log ?? (_ => { });
        var interval = settingsCheckInterval ?? TimeSpan.FromSeconds(1);
        settingsTimer = new Timer(state => _ = ReloadConfigurationAsync(), null, interval, interval);
    }

    public event Action? Updated;

    public AIUsageMonitorSnapshot Snapshot
    {
        get { lock (gate) { return snapshot; } }
    }

    public Task RefreshAsync() => RefreshAsync(onlyIfConfigurationChanged: false);

    public Task ReloadConfigurationAsync() => RefreshAsync(onlyIfConfigurationChanged: true);

    private async Task RefreshAsync(bool onlyIfConfigurationChanged)
    {
        CancellationTokenSource? request = null;
        AIUsageConfiguration? requestedConfiguration;
        bool changed;
        lock (gate)
        {
            if (disposed) return;
            requestedConfiguration = repository.LoadAIUsageConfiguration();
            changed = !SameConfiguration(configuration, requestedConfiguration);
            if (requestedConfiguration is null)
            {
                changed |= currentRequest is not null || snapshot.Bundles is not { Count: 0 };
                currentRequest?.Cancel();
                currentRequest = null;
                configuration = null;
                snapshot = new([], null);
            }
            else
            {
                if (!changed && (onlyIfConfigurationChanged || currentRequest is not null)) return;
                currentRequest?.Cancel();
                currentRequest = request = new CancellationTokenSource();
                configuration = requestedConfiguration;
                if (changed)
                {
                    snapshot = new(snapshot.Bundles?.Where(bundle => IsEnabled(bundle, requestedConfiguration)).ToArray(), null);
                }
            }
        }
        if (changed) Updated?.Invoke();
        if (request is null || requestedConfiguration is null) return;
        var reload = false;
        var published = false;
        try
        {
            request.Token.ThrowIfCancellationRequested();
            var bundles = await refresh(requestedConfiguration, request.Token).ConfigureAwait(false);
            lock (gate)
            {
                if (!disposed && currentRequest == request && !request.IsCancellationRequested)
                {
                    reload = !SameConfiguration(requestedConfiguration, repository.LoadAIUsageConfiguration());
                    if (!reload)
                    {
                        snapshot = new(bundles, DateTimeOffset.Now);
                        published = true;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) { log($"Refreshing AI usage failed: {error.Message}"); }
        finally
        {
            lock (gate)
            {
                if (currentRequest == request) currentRequest = null;
                request.Dispose();
            }
        }
        if (published) Updated?.Invoke();
        if (reload) await ReloadConfigurationAsync().ConfigureAwait(false);
    }

    private static bool SameConfiguration(AIUsageConfiguration? left, AIUsageConfiguration? right) =>
        left is null ? right is null : right is not null && left.Overrides.Count == right.Overrides.Count
            && left.Overrides.All(pair => right.Overrides.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool IsEnabled(AIUsageBundle bundle, AIUsageConfiguration configuration)
    {
        var source = bundle.AccountID is not null ? AIUsageSource.OpenCodex : bundle.Provider switch
        {
            AIUsageProvider.ClaudeCode => AIUsageSource.ClaudeCode,
            AIUsageProvider.Codex => AIUsageSource.Codex,
            AIUsageProvider.Grok => AIUsageSource.Grok,
            _ => throw new ArgumentOutOfRangeException(nameof(bundle)),
        };
        return configuration.IsEnabled(source, isDetected: true);
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            settingsTimer.Dispose();
            currentRequest?.Cancel();
            currentRequest = null;
        }
    }
}
