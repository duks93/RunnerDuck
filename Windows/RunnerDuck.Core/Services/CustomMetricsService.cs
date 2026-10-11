/*
 CustomMetricsService.cs
 RunnerDuck.Core

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

namespace RunnerDuck.Core;

public sealed class CustomMetricsService(CustomMetricsEnvironment environment) : IDisposable
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RewatchDelay = TimeSpan.FromMilliseconds(200);

    private readonly Lock gate = new();
    private readonly Dictionary<Guid, CancellationTokenSource> observers = [];
    private List<CustomMetricsSource> sources = [];
    private List<CustomMetricsBundle> bundles = [];

    public event Action<IReadOnlyList<CustomMetricsBundle>>? BundlesChanged;

    public IReadOnlyList<CustomMetricsBundle> Bundles
    {
        get
        {
            lock (gate)
            {
                return bundles.ToList();
            }
        }
    }

    public CustomMetricsSource CreateSource(string filePath)
    {
        byte[] data;
        try
        {
            data = environment.ReadFile(filePath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CustomMetricsException(CustomMetricsError.FileUnreadable);
        }
        var snapshot = CustomMetricsSnapshot.Decode(data);
        return new CustomMetricsSource
        {
            ID = environment.NewID(),
            DisplayName = snapshot.Title,
            Symbol = snapshot.Symbol,
            FilePath = filePath,
            CreatedAt = environment.Now(),
        };
    }

    public void Reconcile(IReadOnlyList<CustomMetricsSource> newSources)
    {
        List<(CustomMetricsSource Source, CancellationToken Token)> addedObservers;
        lock (gate)
        {
            sources = newSources.ToList();
            var desiredIDs = sources.Select(source => source.ID).ToHashSet();
            foreach (var staleID in observers.Keys.Where(id => !desiredIDs.Contains(id)).ToList())
            {
                observers[staleID].Cancel();
                observers.Remove(staleID);
            }
            bundles = SortedBySourceOrder(bundles);
            addedObservers = sources
                .Where(source => !observers.ContainsKey(source.ID))
                .Select(source =>
                {
                    var cancellation = new CancellationTokenSource();
                    observers[source.ID] = cancellation;
                    return (source, cancellation.Token);
                })
                .ToList();
        }
        NotifyBundlesChanged();
        addedObservers.ForEach(observer => _ = Task.Run(() => ObserveAsync(observer.Source, observer.Token)));
    }

    public void Stop()
    {
        lock (gate)
        {
            foreach (var cancellation in observers.Values)
            {
                cancellation.Cancel();
            }
            observers.Clear();
            sources = [];
            bundles = [];
        }
        NotifyBundlesChanged();
    }

    public void Dispose() => Stop();

    private async Task ObserveAsync(CustomMetricsSource source, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!environment.FileExists(source.FilePath))
                {
                    EmitFailure(source, cancellationToken);
                    await environment.Delay(RetryInterval, cancellationToken);
                    continue;
                }
                LoadSnapshot(source, cancellationToken);
                await foreach (var _ in environment.Watch(source.FilePath, cancellationToken))
                {
                    LoadSnapshot(source, cancellationToken);
                }
                await environment.Delay(RewatchDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                environment.Log($"Watching custom metrics source {source.ID} failed: {error.Message}");
                EmitFailure(source, cancellationToken);
                try
                {
                    await environment.Delay(RetryInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private void LoadSnapshot(CustomMetricsSource source, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = CustomMetricsSnapshot.Decode(environment.ReadFile(source.FilePath));
            Emit(source, cancellationToken, bundle => new CustomMetricsBundle(source.ID, snapshot));
        }
        catch (Exception error) when (error is CustomMetricsException or IOException or UnauthorizedAccessException)
        {
            EmitFailure(source, cancellationToken);
        }
    }

    private void EmitFailure(CustomMetricsSource source, CancellationToken cancellationToken) =>
        Emit(source, cancellationToken, bundle => bundle is null
            ? new CustomMetricsBundle(source.ID, Placeholder(source), IsFailed: true)
            : bundle with { IsFailed = true });

    private void Emit(
        CustomMetricsSource source,
        CancellationToken cancellationToken,
        Func<CustomMetricsBundle?, CustomMetricsBundle> update
    )
    {
        lock (gate)
        {
            if (cancellationToken.IsCancellationRequested || !observers.ContainsKey(source.ID))
            {
                return;
            }
            var index = bundles.FindIndex(bundle => bundle.ID == source.ID);
            if (index >= 0)
            {
                bundles[index] = update(bundles[index]);
            }
            else
            {
                bundles.Add(update(null));
            }
            bundles = SortedBySourceOrder(bundles);
        }
        NotifyBundlesChanged();
    }

    private List<CustomMetricsBundle> SortedBySourceOrder(List<CustomMetricsBundle> currentBundles) =>
        sources
            .Select(source => currentBundles.FirstOrDefault(bundle => bundle.ID == source.ID))
            .OfType<CustomMetricsBundle>()
            .ToList();

    private static CustomMetricsSnapshot Placeholder(CustomMetricsSource source) => new()
    {
        Title = source.DisplayName,
        Symbol = source.Symbol,
        Metrics = [],
        LastUpdatedDate = source.CreatedAt,
    };

    private void NotifyBundlesChanged() => BundlesChanged?.Invoke(Bundles);
}
