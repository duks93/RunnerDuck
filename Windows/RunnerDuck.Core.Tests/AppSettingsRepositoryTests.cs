using System.Text.Json;
using RunnerDuck.AIUsage;

namespace RunnerDuck.Core.Tests;

public sealed class AppSettingsRepositoryTests
{
    [Fact]
    public void Defaults_match_all_mac_settings()
    {
        AssertDefaults(new AppSettings());
    }

    [Fact]
    public void Load_returns_defaults_without_creating_a_missing_file()
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        AssertDefaults(new AppSettingsRepository(path).Load());
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"runnerID\":42}")]
    [InlineData("{\"runnerID\":null}")]
    [InlineData("{\"speedDecreasesUnderLoad\":\"yes\"}")]
    [InlineData("{\"systemMetricsConfiguration\":null}")]
    [InlineData("{\"metricsBarConfiguration\":null}")]
    [InlineData("{\"customMetricsConfiguration\":null}")]
    [InlineData("{\"aiUsageConfiguration\":null}")]
    [InlineData("{\"metricsBarConfiguration\":{\"visibleCustomMetricsSourceIDs\":[\"bad-guid\"]}}")]
    [InlineData("{\"metricsBarConfiguration\":{\"visibleCustomMetricsSourceIDs\":null}}")]
    [InlineData("{\"customMetricsConfiguration\":{\"sources\":null}}")]
    [InlineData("{\"customMetricsConfiguration\":{\"sources\":[null]}}")]
    [InlineData("{\"customMetricsConfiguration\":{\"sources\":[{}]}}")]
    [InlineData("{\"aiUsageConfiguration\":{\"overrides\":null}}")]
    public void Load_returns_defaults_for_corrupt_settings(string json)
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        File.WriteAllText(path, json);
        AssertDefaults(new AppSettingsRepository(path).Load());
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Load_defaults_missing_fields_and_ignores_unknown_fields()
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        File.WriteAllText(path, """
            {
              "runnerID": "custom-id",
              "systemMetricsConfiguration": { "monitorsNetwork": false },
              "metricsBarConfiguration": {},
              "customMetricsConfiguration": {},
              "aiUsageConfiguration": {},
              "futureSetting": true
            }
            """);
        var settings = new AppSettingsRepository(path).Load();
        Assert.Equal("custom-id", settings.RunnerID);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsMemory);
        Assert.False(settings.SystemMetricsConfiguration.MonitorsNetwork);
        settings.RunnerID = "duck";
        settings.SystemMetricsConfiguration.MonitorsNetwork = true;
        AssertDefaults(settings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Load_falls_back_to_five_seconds_for_unknown_interval(int seconds)
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        File.WriteAllText(path, $$"""{"runnerID":"custom-id","updateInterval":{{seconds}}}""");
        var settings = new AppSettingsRepository(path).Load();
        Assert.Equal(UpdateInterval.FiveSeconds, settings.UpdateInterval);
        Assert.Equal("custom-id", settings.RunnerID);
    }

    [Fact]
    public void Load_recovers_each_setting_independently_like_mac_user_defaults()
    {
        using var directory = new TestDirectory();
        var path = directory.File(AppSettingsRepository.FileName);
        var json = """
            {
              "runnerID": "custom-id",
              "speedDecreasesUnderLoad": "yes",
              "isFlippedHorizontally": true,
              "updateInterval": 10,
              "showsMetricsBar": true,
              "systemMetricsConfiguration": { "monitorsBattery": false },
              "metricsBarConfiguration": { "showsMemory": true, "visibleCustomMetricsSourceIDs": ["bad-guid"] },
              "customMetricsConfiguration": { "sources": [null] },
              "aiUsageConfiguration": null
            }
            """;
        File.WriteAllText(path, json);
        var settings = new AppSettingsRepository(path).Load();
        Assert.Equal("custom-id", settings.RunnerID);
        Assert.False(settings.SpeedDecreasesUnderLoad);
        Assert.True(settings.IsFlippedHorizontally);
        Assert.Equal(UpdateInterval.TenSeconds, settings.UpdateInterval);
        Assert.True(settings.ShowsMetricsBar);
        Assert.False(settings.SystemMetricsConfiguration.MonitorsBattery);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsMemory);
        Assert.False(settings.MetricsBarConfiguration.ShowsMemory);
        Assert.True(settings.MetricsBarConfiguration.ShowsCPU);
        Assert.Empty(settings.CustomMetricsConfiguration.Sources);
        Assert.Empty(settings.AIUsageConfiguration.Overrides);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Save_and_load_keep_boolean_settings_distinct()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(directory.File(AppSettingsRepository.FileName));
        repository.Save(new()
        {
            SpeedDecreasesUnderLoad = true,
            IsFlippedHorizontally = false,
            ShowsMetricsBar = true,
            SystemMetricsConfiguration = new() { MonitorsMemory = false, MonitorsStorage = true, MonitorsBattery = false, MonitorsNetwork = true },
            MetricsBarConfiguration = new() { ShowsCPU = false, ShowsMemory = true, ShowsStorage = false, ShowsBattery = true, ShowsNetwork = false },
        });
        var restored = repository.Load();
        Assert.Equal(
            new[] { true, false, true },
            new[] { restored.SpeedDecreasesUnderLoad, restored.IsFlippedHorizontally, restored.ShowsMetricsBar }
        );
        var system = restored.SystemMetricsConfiguration;
        Assert.Equal(new[] { false, true, false, true }, new[] { system.MonitorsMemory, system.MonitorsStorage, system.MonitorsBattery, system.MonitorsNetwork });
        var bar = restored.MetricsBarConfiguration;
        Assert.Equal(new[] { false, true, false, true, false }, new[] { bar.ShowsCPU, bar.ShowsMemory, bar.ShowsStorage, bar.ShowsBattery, bar.ShowsNetwork });
    }

    [Theory]
    [InlineData(UpdateInterval.ThreeSeconds, 3)]
    [InlineData(UpdateInterval.FiveSeconds, 5)]
    [InlineData(UpdateInterval.TenSeconds, 10)]
    public void Save_and_load_round_trip_all_settings_at_injected_path(UpdateInterval interval, int expectedSeconds)
    {
        using var directory = new TestDirectory();
        var path = directory.File("nested/settings.json");
        var firstID = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondID = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var source = new CustomMetricsSource
        {
            ID = firstID,
            DisplayName = "빌드",
            Symbol = "hammer",
            FilePath = @"C:\사용자 데이터\build.json",
            CreatedAt = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.FromHours(9)),
        };
        var settings = new AppSettings
        {
            RunnerID = "custom-runner",
            SpeedDecreasesUnderLoad = true,
            IsFlippedHorizontally = true,
            UpdateInterval = interval,
            ShowsMetricsBar = true,
            SystemMetricsConfiguration = new()
            {
                MonitorsMemory = false,
                MonitorsStorage = false,
                MonitorsBattery = false,
                MonitorsNetwork = false,
            },
            MetricsBarConfiguration = new()
            {
                ShowsCPU = false,
                ShowsMemory = true,
                ShowsStorage = true,
                ShowsBattery = true,
                ShowsNetwork = true,
                VisibleCustomMetricsSourceIDs = [firstID, secondID],
            },
            CustomMetricsConfiguration = new()
            {
                Sources = [source, source with { ID = secondID, DisplayName = "Other", Symbol = null }],
            },
        };
        settings.AIUsageConfiguration.Set(AIUsageSource.Codex, false);
        settings.AIUsageConfiguration.Set(AIUsageSource.OpenCodex, true);
        new AppSettingsRepository(path).Save(settings);

        var restored = new AppSettingsRepository(path).Load();
        Assert.Equal("custom-runner", restored.RunnerID);
        Assert.True(restored.SpeedDecreasesUnderLoad);
        Assert.True(restored.IsFlippedHorizontally);
        Assert.Equal(interval, restored.UpdateInterval);
        Assert.True(restored.ShowsMetricsBar);
        Assert.False(restored.SystemMetricsConfiguration.MonitorsMemory);
        Assert.False(restored.SystemMetricsConfiguration.MonitorsStorage);
        Assert.False(restored.SystemMetricsConfiguration.MonitorsBattery);
        Assert.False(restored.SystemMetricsConfiguration.MonitorsNetwork);
        Assert.False(restored.MetricsBarConfiguration.ShowsCPU);
        Assert.True(restored.MetricsBarConfiguration.ShowsMemory);
        Assert.True(restored.MetricsBarConfiguration.ShowsStorage);
        Assert.True(restored.MetricsBarConfiguration.ShowsBattery);
        Assert.True(restored.MetricsBarConfiguration.ShowsNetwork);
        Assert.True(restored.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs.SetEquals([firstID, secondID]));
        Assert.Equal(settings.CustomMetricsConfiguration.Sources, restored.CustomMetricsConfiguration.Sources);
        Assert.False(restored.AIUsageConfiguration.IsEnabled(AIUsageSource.Codex, true));
        Assert.True(restored.AIUsageConfiguration.IsEnabled(AIUsageSource.OpenCodex, false));

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(expectedSeconds, json.RootElement.GetProperty("updateInterval").GetInt32());
        Assert.Equal("custom-runner", json.RootElement.GetProperty("runnerID").GetString());
        Assert.False(json.RootElement.GetProperty("aiUsageConfiguration").GetProperty("overrides").GetProperty("codex").GetBoolean());
        var storedSource = json.RootElement.GetProperty("customMetricsConfiguration").GetProperty("sources")[0];
        Assert.Equal(firstID, storedSource.GetProperty("id").GetGuid());
        Assert.Equal(source.FilePath, storedSource.GetProperty("filePath").GetString());
        Assert.False(storedSource.TryGetProperty("bookmark", out _));
        Assert.Single(Directory.GetFiles(System.IO.Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void Defaults_have_independent_mutable_collections()
    {
        var first = new AppSettings();
        first.SystemMetricsConfiguration.MonitorsMemory = false;
        first.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs.Add(Guid.NewGuid());
        first.CustomMetricsConfiguration.Sources.Add(new()
        {
            ID = Guid.NewGuid(),
            DisplayName = "Test",
            FilePath = "test.json",
            CreatedAt = DateTimeOffset.UnixEpoch,
        });
        first.AIUsageConfiguration.Set(AIUsageSource.Codex, false);
        AssertDefaults(new AppSettings());
    }

    [Fact]
    public void Save_replaces_previous_settings()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(directory.File(AppSettingsRepository.FileName));
        repository.Save(new() { RunnerID = "custom-id", ShowsMetricsBar = true });
        repository.Save(new());
        AssertDefaults(repository.Load());
    }

    [Fact]
    public void Save_propagates_write_errors()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(directory.Path);
        var error = Record.Exception(() => repository.Save(new()));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    private static void AssertDefaults(AppSettings settings)
    {
        Assert.Equal("duck", settings.RunnerID);
        Assert.False(settings.SpeedDecreasesUnderLoad);
        Assert.False(settings.IsFlippedHorizontally);
        Assert.Equal(UpdateInterval.FiveSeconds, settings.UpdateInterval);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsMemory);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsStorage);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsBattery);
        Assert.True(settings.SystemMetricsConfiguration.MonitorsNetwork);
        Assert.False(settings.ShowsMetricsBar);
        Assert.True(settings.MetricsBarConfiguration.ShowsCPU);
        Assert.False(settings.MetricsBarConfiguration.ShowsMemory);
        Assert.False(settings.MetricsBarConfiguration.ShowsStorage);
        Assert.False(settings.MetricsBarConfiguration.ShowsBattery);
        Assert.False(settings.MetricsBarConfiguration.ShowsNetwork);
        Assert.Empty(settings.MetricsBarConfiguration.VisibleCustomMetricsSourceIDs);
        Assert.Empty(settings.CustomMetricsConfiguration.Sources);
        Assert.Empty(settings.AIUsageConfiguration.Overrides);
    }
}
