using System.Text.Json;
using static RunnerDuck.AIUsage.Tests.TestEnvironment;

namespace RunnerDuck.AIUsage.Tests;

public sealed class AIUsageSourceSettingsTests
{
    private const string CodexAuthJSON = """{ "tokens": { "access_token": "token" } }""";

    [Fact]
    public void SourceStates_enables_detected_sources_unless_overridden()
    {
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = CodexAuthJSON },
            pathExists: path => path.EndsWith(".opencodex", StringComparison.Ordinal)
        ));
        Assert.Equal(
            new[]
            {
                new AIUsageSourceState(AIUsageSource.ClaudeCode, false, false),
                new AIUsageSourceState(AIUsageSource.Codex, true, true),
                new AIUsageSourceState(AIUsageSource.Grok, false, false),
                new AIUsageSourceState(AIUsageSource.OpenCodex, true, true),
            },
            sut.SourceStates()
        );
        var configuration = new AIUsageConfiguration();
        configuration.Set(AIUsageSource.Codex, false);
        configuration.Set(AIUsageSource.Grok, true);
        Assert.Equal(new[] { false, false, true, true }, sut.SourceStates(configuration).Select(state => state.IsEnabled));
        Assert.Equal(new[] { false, true, false, true }, sut.SourceStates(configuration).Select(state => state.IsDetected));
    }

    [Fact]
    public async Task Refresh_skips_disabled_sources()
    {
        var handler = new StubHttpHandler(_ => throw new HttpRequestException("should not be called"));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = CodexAuthJSON },
            handler: handler
        ));
        var configuration = new AIUsageConfiguration();
        configuration.Set(AIUsageSource.Codex, false);
        Assert.Empty(await sut.RefreshAsync(configuration));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Refresh_without_configuration_uses_detected_sources()
    {
        var handler = new StubHttpHandler(_ => Response(401));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = CodexAuthJSON },
            handler: handler
        ));
        var bundle = Assert.Single(await sut.RefreshAsync());
        Assert.Equal(AIUsageStatus.SignInRequired, bundle.Status);
        Assert.Single(handler.Requests);
    }

    private const string ClaudeCodeCredentialsJSON = """{ "claudeAiOauth": { "accessToken": "claude-token", "expiresAt": 1790000000000 } }""";
    private const string GrokAuthJSON = """{ "https://auth.x.ai::client": { "key": "grok-token" } }""";

    [Theory]
    [InlineData(AIUsageSource.ClaudeCode, "api.anthropic.com")]
    [InlineData(AIUsageSource.Codex, "chatgpt.com")]
    [InlineData(AIUsageSource.Grok, "cli-chat-proxy.grok.com")]
    [InlineData(AIUsageSource.OpenCodex, "127.0.0.1")]
    public async Task Refresh_sends_no_request_for_each_disabled_source(AIUsageSource disabledSource, string disabledHost)
    {
        var handler = new StubHttpHandler(_ => Response(401));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".claude/.credentials.json"] = ClaudeCodeCredentialsJSON,
                [".codex/auth.json"] = CodexAuthJSON,
                [".grok/auth.json"] = GrokAuthJSON,
            },
            handler: handler,
            pathExists: path => path.EndsWith(".opencodex", StringComparison.Ordinal)
        ));
        var configuration = new AIUsageConfiguration();
        configuration.Set(disabledSource, false);
        await sut.RefreshAsync(configuration);
        var hosts = handler.Requests.Select(request => request.RequestUri!.Host).Distinct().ToList();
        Assert.DoesNotContain(disabledHost, hosts);
        Assert.Equal(3, hosts.Count);
    }

    [Fact]
    public async Task Enabled_local_grok_login_hides_matching_opencodex_account_and_main_account()
    {
        var sameID = AIUsageService.IdentityHash("user-1")[..8];
        var handler = new StubHttpHandler(request => request.RequestUri?.Host == "127.0.0.1"
            ? request.RequestUri.Query.Contains("xai")
                ? Response(200, $$"""
                    { "accounts": [
                      { "id": "{{sameID}}", "quota": { "weeklyPercent": 1 } },
                      { "id": "main-account", "isMain": true, "quota": { "weeklyPercent": 2 } },
                      { "id": "other123", "quota": { "weeklyPercent": 3 } }
                    ] }
                    """)
                : Response(200, """{ "accounts": [] }""")
            : Response(401));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".grok/auth.json"] = """{ "https://auth.x.ai::client": { "key": "token", "user_id": "user-1" } }""",
            },
            handler: handler,
            pathExists: path => path.EndsWith(".opencodex", StringComparison.Ordinal)
        ));
        var bundles = await sut.RefreshAsync(new AIUsageConfiguration());
        Assert.Equal(new string?[] { null, "openCodex/other123" }, bundles.Select(bundle => bundle.AccountID));
        Assert.Equal(AIUsageStatus.SignInRequired, bundles[0].Status);
    }

    [Fact]
    public async Task Disabled_local_grok_login_does_not_hide_matching_opencodex_account()
    {
        var sameID = AIUsageService.IdentityHash("user-1")[..8];
        var handler = new StubHttpHandler(request => request.RequestUri?.Query.Contains("xai") == true
            ? Response(200, $$"""{ "accounts": [ { "id": "{{sameID}}", "quota": { "weeklyPercent": 1 } } ] }""")
            : Response(200, """{ "accounts": [] }"""));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".grok/auth.json"] = """{ "https://auth.x.ai::client": { "key": "token", "user_id": "user-1" } }""",
            },
            handler: handler,
            pathExists: path => path.EndsWith(".opencodex", StringComparison.Ordinal)
        ));
        var configuration = new AIUsageConfiguration();
        configuration.Set(AIUsageSource.Grok, false);
        var bundle = Assert.Single(await sut.RefreshAsync(configuration));
        Assert.Equal($"openCodex/{sameID}", bundle.AccountID);
    }
}

public sealed class AIUsageSnapshotRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset SavedAt = new(2026, 10, 6, 12, 0, 0, TimeSpan.FromHours(9));

    private readonly string directory = Path.Combine(AppContext.BaseDirectory, "snapshot-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(directory, AIUsageSnapshotRepository.FileName);

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AIUsageSnapshot Snapshot() => new(
        SavedAt,
        [
            new(
                AIUsageProvider.ClaudeCode,
                AIUsageStatus.Available,
                [
                    new(AIUsageWindowKind.FiveHour, 42.5, SavedAt.AddHours(2)),
                    new(AIUsageWindowKind.WeeklyModel("Opus"), 10),
                ],
                SavedAt
            ),
            new(AIUsageProvider.Codex, AIUsageStatus.SignInRequired, [], SavedAt, "openCodex/sub", "sub-1"),
            new(AIUsageProvider.Grok, AIUsageStatus.Failed, [], SavedAt),
        ]
    );

    [Fact]
    public void Save_and_load_round_trip_snapshot()
    {
        var sut = new AIUsageSnapshotRepository(FilePath);
        sut.Save(Snapshot());
        Assert.Equal(Snapshot(), new AIUsageSnapshotRepository(FilePath).Load());
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void Saved_json_uses_readable_enum_names()
    {
        new AIUsageSnapshotRepository(FilePath).Save(Snapshot());
        using var json = JsonDocument.Parse(File.ReadAllText(FilePath));
        var bundles = json.RootElement.GetProperty("bundles");
        Assert.Equal("claudeCode", bundles[0].GetProperty("provider").GetString());
        Assert.Equal("signInRequired", bundles[1].GetProperty("status").GetString());
        Assert.Equal("weeklyModel", bundles[0].GetProperty("windows")[1].GetProperty("kind").GetProperty("type").GetString());
        foreach (var computedProperty in new[] { "id", "displayName", "summaryWindows" })
        {
            Assert.False(bundles[0].TryGetProperty(computedProperty, out _), computedProperty);
        }
        Assert.False(bundles[0].GetProperty("windows")[0].TryGetProperty("remainingPercentage", out _));
    }

    [Fact]
    public void Save_overwrites_previous_snapshot_including_empty_bundles()
    {
        var sut = new AIUsageSnapshotRepository(FilePath);
        sut.Save(Snapshot());
        var empty = new AIUsageSnapshot(SavedAt.AddMinutes(5), []);
        sut.Save(empty);
        Assert.Equal(empty, new AIUsageSnapshotRepository(FilePath).Load());
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{ "savedAt": "2026-10-06T03:00:00Z", "bundles": [ null ] }""")]
    [InlineData("""{ "savedAt": "2026-10-06T03:00:00Z", "bundles": [ { "provider": "unknown", "status": "available", "windows": [], "lastUpdatedDate": "2026-10-06T03:00:00Z" } ] }""")]
    [InlineData("""{ "bundles": [] }""")]
    public void Load_returns_null_for_corrupt_snapshot(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, json);
        Assert.Null(new AIUsageSnapshotRepository(FilePath).Load());
    }

    [Fact]
    public void Load_returns_null_when_file_is_missing()
    {
        Assert.Null(new AIUsageSnapshotRepository(FilePath).Load());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(360, true)]
    [InlineData(361, false)]
    [InlineData(-1, false)]
    public void IsFresh_accepts_snapshots_up_to_six_minutes_old(int ageSeconds, bool expected)
    {
        Assert.Equal(expected, Snapshot().IsFresh(SavedAt.AddSeconds(ageSeconds)));
    }
}
