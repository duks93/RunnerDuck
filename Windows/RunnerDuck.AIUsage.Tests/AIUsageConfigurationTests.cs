using System.Text.Json;

namespace RunnerDuck.AIUsage.Tests;

public sealed class AIUsageConfigurationTests
{
    [Theory]
    [InlineData(AIUsageSource.ClaudeCode, "claudeCode")]
    [InlineData(AIUsageSource.Codex, "codex")]
    [InlineData(AIUsageSource.Grok, "grok")]
    [InlineData(AIUsageSource.OpenCodex, "openCodex")]
    public void Configuration_uses_raw_source_keys_and_overrides_detection(AIUsageSource source, string key)
    {
        var configuration = new AIUsageConfiguration();
        Assert.Empty(configuration.Overrides);
        Assert.False(configuration.IsEnabled(source, false));
        Assert.True(configuration.IsEnabled(source, true));
        configuration.Set(source, false);
        Assert.False(configuration.IsEnabled(source, true));
        Assert.False(configuration.IsEnabled(source, false));
        Assert.Equal(new KeyValuePair<string, bool>(key, false), Assert.Single(configuration.Overrides));
        configuration.Set(source, true);
        Assert.True(configuration.IsEnabled(source, false));
        Assert.True(configuration.IsEnabled(source, true));
        Assert.Single(configuration.Overrides);
    }

    [Fact]
    public void IsEnabled_matches_mac_detected_and_overridden_source_states()
    {
        var configuration = new AIUsageConfiguration();
        var sources = new[] { AIUsageSource.ClaudeCode, AIUsageSource.Codex, AIUsageSource.Grok, AIUsageSource.OpenCodex };
        var detected = new[] { false, true, false, true };
        Assert.Equal(detected, sources.Select((source, index) => configuration.IsEnabled(source, detected[index])));
        configuration.Set(AIUsageSource.Codex, false);
        configuration.Set(AIUsageSource.Grok, true);
        Assert.Equal(
            new[] { false, false, true, true },
            sources.Select((source, index) => configuration.IsEnabled(source, detected[index]))
        );
    }

    [Fact]
    public void Configuration_round_trips_mac_json_and_preserves_unknown_keys()
    {
        var configuration = JsonSerializer.Deserialize<AIUsageConfiguration>("""
            {"overrides":{"claudeCode":false,"codex":true,"grok":false,"openCodex":true,"futureSource":false}}
            """)!;
        Assert.False(configuration.IsEnabled(AIUsageSource.ClaudeCode, true));
        Assert.True(configuration.IsEnabled(AIUsageSource.Codex, false));
        Assert.False(configuration.IsEnabled(AIUsageSource.Grok, true));
        Assert.True(configuration.IsEnabled(AIUsageSource.OpenCodex, false));
        var restored = JsonSerializer.Deserialize<AIUsageConfiguration>(JsonSerializer.Serialize(configuration))!;
        Assert.Equal(configuration.Overrides.OrderBy(pair => pair.Key), restored.Overrides.OrderBy(pair => pair.Key));
        Assert.False(restored.Overrides["futureSource"]);
    }

    [Fact]
    public void Empty_json_uses_defaults_and_instances_do_not_share_overrides()
    {
        var first = JsonSerializer.Deserialize<AIUsageConfiguration>("{}")!;
        Assert.Empty(first.Overrides);
        Assert.True(first.IsEnabled(AIUsageSource.Codex, true));
        Assert.False(first.IsEnabled(AIUsageSource.Codex, false));
        first.Set(AIUsageSource.Codex, true);
        Assert.Empty(new AIUsageConfiguration().Overrides);
    }
}
