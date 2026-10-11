using System.Collections.Concurrent;

namespace RunnerDuck.Core.Tests;

public sealed class CustomMetricsServiceTests
{
    private const string FilePath = @"C:\metrics\usage.json";
    private const string SecondPath = @"C:\metrics\second.json";

    private static CustomMetricsSource Source(Guid id, string filePath = FilePath, string displayName = "Card") => new()
    {
        ID = id,
        DisplayName = displayName,
        Symbol = "gauge",
        FilePath = filePath,
        CreatedAt = FakeCustomMetricsFiles.Now.AddDays(-1),
    };

    private static CustomMetricsSnapshot Decoded(string json) =>
        CustomMetricsSnapshot.Decode(System.Text.Encoding.UTF8.GetBytes(json));

    [Fact]
    public void CreateSource_builds_source_from_snapshot()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Build"));
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var source = new CustomMetricsService(files.Environment(id)).CreateSource(FilePath);
        Assert.Equal(
            new CustomMetricsSource { ID = id, DisplayName = "Build", Symbol = "gauge", FilePath = FilePath, CreatedAt = FakeCustomMetricsFiles.Now },
            source
        );
    }

    [Fact]
    public void CreateSource_throws_FileUnreadable_when_read_fails()
    {
        var error = Assert.Throws<CustomMetricsException>(() =>
            new CustomMetricsService(new FakeCustomMetricsFiles().Environment()).CreateSource(FilePath));
        Assert.Equal(CustomMetricsError.FileUnreadable, error.Error);
    }

    [Fact]
    public void CreateSource_throws_FileUnreadable_when_access_is_denied()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Denied"));
        files.DenyReading(FilePath, true);
        var error = Assert.Throws<CustomMetricsException>(() => new CustomMetricsService(files.Environment()).CreateSource(FilePath));
        Assert.Equal(CustomMetricsError.FileUnreadable, error.Error);
    }

    [Fact]
    public void CreateSource_throws_InvalidFormat_when_decode_fails()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, "{}");
        var error = Assert.Throws<CustomMetricsException>(() => new CustomMetricsService(files.Environment()).CreateSource(FilePath));
        Assert.Equal(CustomMetricsError.InvalidFormat, error.Error);
    }

    [Fact]
    public async Task Reconcile_observes_source_and_notifies_reloaded_snapshot_when_file_changes()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("First", "1"));
        var id = Guid.NewGuid();
        using var sut = new CustomMetricsService(files.Environment());
        var notifiedTitles = new ConcurrentQueue<string>();
        sut.BundlesChanged += bundles =>
        {
            foreach (var bundle in bundles)
            {
                notifiedTitles.Enqueue(bundle.Snapshot.Title);
            }
        };
        sut.Reconcile([Source(id)]);
        await Wait.UntilAsync(() => files.IsWatching(FilePath) && sut.Bundles is [{ Snapshot.Title: "First" }], "the first snapshot is watched");
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Second", "2"));
        await Wait.UntilAsync(() => sut.Bundles is [{ Snapshot.Title: "Second" }], "the changed snapshot is loaded");
        Assert.Equal(new CustomMetricsBundle(id, Decoded(FakeCustomMetricsFiles.SnapshotJSON("Second", "2"))), sut.Bundles[0]);
        Assert.Contains("First", notifiedTitles);
        Assert.Contains("Second", notifiedTitles);
    }

    [Fact]
    public async Task Change_between_first_read_and_watch_start_is_loaded()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Before watch"));
        files.OnWatchStarting = path => files.Write(path, FakeCustomMetricsFiles.SnapshotJSON("Changed before watch"), notifies: false);
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(Guid.NewGuid())]);
        await Wait.UntilAsync(() => sut.Bundles is [{ Snapshot.Title: "Changed before watch" }], "the change before the watch is loaded");
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("""{ "metrics": [] }""")]
    public async Task Invalid_json_keeps_previous_snapshot_marked_failed_until_fixed(string invalidJSON)
    {
        var files = new FakeCustomMetricsFiles();
        var goodJSON = FakeCustomMetricsFiles.SnapshotJSON("Good", "7");
        files.Write(FilePath, goodJSON);
        var id = Guid.NewGuid();
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(id)]);
        await Wait.UntilAsync(() => files.IsWatching(FilePath) && sut.Bundles.Count == 1, "the file is watched");
        files.Write(FilePath, invalidJSON);
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: true }], "the bundle is marked failed");
        Assert.Equal(new CustomMetricsBundle(id, Decoded(goodJSON), IsFailed: true), sut.Bundles[0]);
        Assert.Equal("---", sut.Bundles[0].MetricsBarLabel);
        var fixedJSON = FakeCustomMetricsFiles.SnapshotJSON("Fixed", "8", "9%");
        files.Write(FilePath, fixedJSON);
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: false }], "the bundle recovers");
        Assert.Equal(new CustomMetricsBundle(id, Decoded(fixedJSON)), sut.Bundles[0]);
    }

    [Fact]
    public async Task Denied_read_marks_failure_and_recovers_on_next_change()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Readable"));
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(Guid.NewGuid())]);
        await Wait.UntilAsync(() => files.IsWatching(FilePath) && sut.Bundles.Count == 1, "the file is watched");
        files.DenyReading(FilePath, true);
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Hidden"));
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: true, Snapshot.Title: "Readable" }], "the denied read is reported");
        files.DenyReading(FilePath, false);
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Readable again"));
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: false, Snapshot.Title: "Readable again" }], "the read recovers");
    }

    [Fact]
    public async Task Missing_file_emits_failed_placeholder_and_retries_every_five_seconds()
    {
        var files = new FakeCustomMetricsFiles();
        var source = Source(Guid.NewGuid(), displayName: "Placeholder");
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([source]);
        await Wait.UntilAsync(() => sut.Bundles.Count == 1, "the placeholder is emitted");
        Assert.Equal(
            new CustomMetricsBundle(
                source.ID,
                new CustomMetricsSnapshot { Title = "Placeholder", Symbol = "gauge", Metrics = [], LastUpdatedDate = source.CreatedAt },
                IsFailed: true
            ),
            sut.Bundles[0]
        );
        await Wait.UntilAsync(() => files.Delays.Count >= 2, "two retries are scheduled");
        Assert.All(files.Delays, delay => Assert.Equal(TimeSpan.FromSeconds(5), delay));
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Appeared"));
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: false, Snapshot.Title: "Appeared" }], "the file is loaded after retry");
    }

    [Fact]
    public async Task Deleted_file_rewatches_after_short_delay_and_reopens_recreated_file()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Before"));
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(Guid.NewGuid())]);
        await Wait.UntilAsync(() => files.IsWatching(FilePath) && sut.Bundles.Count == 1, "the file is watched");
        Assert.Empty(files.Delays);
        files.Delete(FilePath);
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: true }], "the deletion is reported");
        Assert.Equal("Before", sut.Bundles[0].Snapshot.Title);
        Assert.Equal(TimeSpan.FromMilliseconds(200), files.Delays[0]);
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("After"));
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: false, Snapshot.Title: "After" }], "the recreated file is loaded");
    }

    [Fact]
    public async Task Deletion_before_watch_starts_is_reported_and_recovered()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Loaded once"));
        var hasDeleted = 0;
        files.OnWatchStarting = path =>
        {
            if (Interlocked.Exchange(ref hasDeleted, 1) == 0)
            {
                files.Delete(path);
            }
        };
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(Guid.NewGuid())]);
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: true, Snapshot.Title: "Loaded once" }], "the early deletion is reported");
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Back"));
        await Wait.UntilAsync(() => sut.Bundles is [{ IsFailed: false, Snapshot.Title: "Back" }], "the file is reopened");
    }

    [Fact]
    public async Task Reconcile_cancels_removed_source_and_stops_reading_it()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Removed"));
        files.Write(SecondPath, FakeCustomMetricsFiles.SnapshotJSON("Kept"));
        var removed = Source(Guid.NewGuid());
        var kept = Source(Guid.NewGuid(), SecondPath);
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([removed, kept]);
        await Wait.UntilAsync(() => sut.Bundles.Count == 2 && files.IsWatching(FilePath), "both sources are watched");
        sut.Reconcile([kept]);
        Assert.Equal(new[] { kept.ID }, sut.Bundles.Select(bundle => bundle.ID));
        await Wait.UntilAsync(() => !files.IsWatching(FilePath), "the removed source watch is cancelled");
        var readCount = files.ReadCount(FilePath);
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Changed after removal"));
        await Task.Delay(50);
        Assert.Equal(readCount, files.ReadCount(FilePath));
        Assert.Equal(new[] { "Kept" }, sut.Bundles.Select(bundle => bundle.Snapshot.Title));
    }

    [Fact]
    public async Task Late_loaded_source_keeps_configured_position()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("First"));
        files.Write(SecondPath, FakeCustomMetricsFiles.SnapshotJSON("Second"));
        using var heldReads = files.HoldReads(FilePath);
        var first = Source(Guid.NewGuid());
        var second = Source(Guid.NewGuid(), SecondPath);
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([first, second]);
        await Wait.UntilAsync(() => sut.Bundles is [{ Snapshot.Title: "Second" }], "the second source loads first");
        heldReads.Set();
        await Wait.UntilAsync(() => sut.Bundles.Count == 2, "the first source loads late");
        Assert.Equal(new[] { first.ID, second.ID }, sut.Bundles.Select(bundle => bundle.ID));
    }

    [Fact]
    public async Task Reconcile_sorts_bundles_by_source_order()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("First"));
        files.Write(SecondPath, FakeCustomMetricsFiles.SnapshotJSON("Second"));
        var first = Source(Guid.NewGuid());
        var second = Source(Guid.NewGuid(), SecondPath);
        using var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([first, second]);
        await Wait.UntilAsync(() => sut.Bundles.Count == 2, "both sources are loaded");
        Assert.Equal(new[] { "First", "Second" }, sut.Bundles.Select(bundle => bundle.Snapshot.Title));
        sut.Reconcile([second, first]);
        Assert.Equal(new[] { "Second", "First" }, sut.Bundles.Select(bundle => bundle.Snapshot.Title));
    }

    [Fact]
    public async Task Stop_cancels_observers_and_clears_bundles()
    {
        var files = new FakeCustomMetricsFiles();
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("Loaded"));
        var sut = new CustomMetricsService(files.Environment());
        sut.Reconcile([Source(Guid.NewGuid())]);
        await Wait.UntilAsync(() => sut.Bundles.Count == 1 && files.IsWatching(FilePath), "the source is loaded");
        sut.Stop();
        Assert.Empty(sut.Bundles);
        await Wait.UntilAsync(() => !files.IsWatching(FilePath), "the watch is cancelled");
        var readCount = files.ReadCount(FilePath);
        files.Write(FilePath, FakeCustomMetricsFiles.SnapshotJSON("After stop"));
        await Task.Delay(50);
        Assert.Equal(readCount, files.ReadCount(FilePath));
        Assert.Empty(sut.Bundles);
    }
}
