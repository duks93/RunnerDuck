using System.Text;
using System.Threading.Channels;

namespace RunnerDuck.Core.Tests;

internal sealed class FakeCustomMetricsFiles
{
    public static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly Lock gate = new();
    private readonly Dictionary<string, string> files = [];
    private readonly Dictionary<string, Channel<DateTimeOffset>> watchers = [];
    private readonly Dictionary<string, int> readCounts = [];
    private readonly Dictionary<string, ManualResetEventSlim> readGates = [];
    private readonly HashSet<string> deniedPaths = [];
    private readonly List<TimeSpan> delays = [];

    public Action<string>? OnWatchStarting { get; set; }

    public IReadOnlyList<TimeSpan> Delays
    {
        get
        {
            lock (gate)
            {
                return delays.ToList();
            }
        }
    }

    public CustomMetricsEnvironment Environment(Guid? newID = null) => new()
    {
        ReadFile = Read,
        FileExists = path =>
        {
            lock (gate)
            {
                return files.ContainsKey(path);
            }
        },
        Watch = StartWatching,
        Delay = (interval, cancellationToken) =>
        {
            lock (gate)
            {
                delays.Add(interval);
            }
            return Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        },
        Now = () => Now,
        NewID = () => newID ?? Guid.NewGuid(),
    };

    public void Write(string path, string content, bool notifies = true)
    {
        lock (gate)
        {
            files[path] = content;
            if (notifies && watchers.TryGetValue(path, out var channel))
            {
                channel.Writer.TryWrite(Now);
            }
        }
    }

    public void Delete(string path)
    {
        lock (gate)
        {
            files.Remove(path);
            if (watchers.Remove(path, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }
    }

    public void DenyReading(string path, bool isDenied)
    {
        lock (gate)
        {
            if (isDenied)
            {
                deniedPaths.Add(path);
            }
            else
            {
                deniedPaths.Remove(path);
            }
        }
    }

    public ManualResetEventSlim HoldReads(string path)
    {
        var readGate = new ManualResetEventSlim(false);
        lock (gate)
        {
            readGates[path] = readGate;
        }
        return readGate;
    }

    public bool IsWatching(string path)
    {
        lock (gate)
        {
            return watchers.ContainsKey(path);
        }
    }

    public int ReadCount(string path)
    {
        lock (gate)
        {
            return readCounts.GetValueOrDefault(path);
        }
    }

    public static string SnapshotJSON(string title, string? metricsBarValue = null, string formattedValue = "1%") => $$"""
        {
          "title": "{{title}}",
          "symbol": "gauge",
          {{(metricsBarValue is null ? "" : $"\"metricsBarValue\": \"{metricsBarValue}\",")}}
          "metrics": [ { "title": "Usage", "formattedValue": "{{formattedValue}}", "normalizedValue": 0.01 } ],
          "lastUpdatedDate": "2026-10-06T03:00:00Z"
        }
        """;

    private byte[] Read(string path)
    {
        ManualResetEventSlim? readGate;
        lock (gate)
        {
            readGate = readGates.GetValueOrDefault(path);
        }
        readGate?.Wait(TimeSpan.FromSeconds(5));
        lock (gate)
        {
            readCounts[path] = readCounts.GetValueOrDefault(path) + 1;
            if (deniedPaths.Contains(path))
            {
                throw new UnauthorizedAccessException(path);
            }
            return files.TryGetValue(path, out var content)
                ? Encoding.UTF8.GetBytes(content)
                : throw new FileNotFoundException(path);
        }
    }

    private IAsyncEnumerable<DateTimeOffset> StartWatching(string path, CancellationToken cancellationToken)
    {
        OnWatchStarting?.Invoke(path);
        var channel = Channel.CreateUnbounded<DateTimeOffset>();
        lock (gate)
        {
            if (files.ContainsKey(path))
            {
                watchers[path] = channel;
                channel.Writer.TryWrite(Now);
            }
            else
            {
                channel.Writer.TryComplete();
            }
        }
        cancellationToken.Register(() =>
        {
            lock (gate)
            {
                if (watchers.TryGetValue(path, out var current) && current == channel)
                {
                    watchers.Remove(path);
                }
            }
            channel.Writer.TryComplete();
        });
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}

internal static class Wait
{
    public static async Task UntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting until {description}.");
            }
            await Task.Delay(5);
        }
    }
}
