using System.Globalization;
using System.Text.Json.Nodes;
using RunnerDuck.AIUsage.Presentation;

namespace RunnerDuck.AIUsage.Tests;

public sealed class UsageCardBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static JsonObject Build(IReadOnlyList<AIUsageBundle>? bundles, UsageStrings? strings = null) =>
        UsageCardBuilder.Build(bundles, Now, Now, strings ?? UsageStrings.English, CultureInfo.InvariantCulture);

    private static AIUsageBundle ClaudeCodeBundle() => new(
        AIUsageProvider.ClaudeCode,
        AIUsageStatus.Available,
        [
            new(AIUsageWindowKind.FiveHour, 42.4, Now.AddHours(2).AddMinutes(5)),
            new(AIUsageWindowKind.Weekly, 95.2),
            new(AIUsageWindowKind.WeeklyModel("Opus"), 10),
        ],
        Now
    );

    [Fact]
    public void Build_without_bundles_shows_loading_message()
    {
        var data = Build(null);
        Assert.Equal("Loading…", data["message"]!.GetValue<string>());
        Assert.True(data["hasMessage"]!.GetValue<bool>());
        Assert.False(data["hasLastUpdated"]!.GetValue<bool>());
        Assert.Empty(data["smallRows"]!.AsArray());
    }

    [Fact]
    public void Build_with_no_bundles_shows_empty_message()
    {
        Assert.Equal("No signed-in AI coding tools found.", Build([])["message"]!.GetValue<string>());
    }

    [Fact]
    public void Build_maps_summary_windows_to_bars()
    {
        var data = Build([ClaudeCodeBundle()]);
        var mediumRow = data["mediumRows"]![0]!;
        Assert.Equal("Claude Code", mediumRow["title"]!.GetValue<string>());
        Assert.False(data["hasMessage"]!.GetValue<bool>());
        Assert.Equal(
            """{"label":"5h","remainingText":"58%","remainingWeight":58,"usedWeight":42,"hasRemaining":true,"hasUsed":true,"barStyle":"accent","resetText":"2h 5m"}""",
            mediumRow["windows"]![0]!.ToJsonString()
        );
        Assert.Equal(
            """{"label":"Wk","remainingText":"5%","remainingWeight":5,"usedWeight":95,"hasRemaining":true,"hasUsed":true,"barStyle":"attention","resetText":""}""",
            mediumRow["windows"]![1]!.ToJsonString()
        );
        Assert.Equal("5h 58% · Wk 5%", data["smallRows"]![0]!["compact"]!.GetValue<string>());
        Assert.Equal(
            new[] { "5h", "Wk", "Opus" },
            data["largeRows"]![0]!["windows"]!.AsArray().Select(window => window!["label"]!.GetValue<string>())
        );
    }

    [Fact]
    public void Build_shows_localized_status_for_expired_sign_in()
    {
        var bundle = new AIUsageBundle(AIUsageProvider.Codex, AIUsageStatus.SignInRequired, [], Now, "openCodex/sub", "sub-1");
        var row = Build([bundle], UsageStrings.Korean)["smallRows"]![0]!;
        Assert.Equal("Codex · sub-1", row["title"]!.GetValue<string>());
        Assert.Equal("로그인 만료", row["compact"]!.GetValue<string>());
        Assert.Equal("warning", row["statusColor"]!.GetValue<string>());
    }

    [Fact]
    public void WithinBudget_stops_before_exceeding_line_budget()
    {
        var bundles = Enumerable.Repeat(ClaudeCodeBundle(), 4).ToList();
        Assert.Equal(2, UsageCardBuilder.WithinBudget(bundles, 8, bundle => bundle.SummaryWindows).Count());
        Assert.Single(UsageCardBuilder.WithinBudget(bundles, 2, bundle => bundle.Windows));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(30, "1m")]
    [InlineData(300, "5m")]
    [InlineData(7_500, "2h 5m")]
    [InlineData(97_200, "1d 3h")]
    public void Duration_formats_remaining_time(int seconds, string expected)
    {
        Assert.Equal(
            expected,
            UsageCardBuilder.Duration(TimeSpan.FromSeconds(seconds), UsageStrings.English, CultureInfo.InvariantCulture)
        );
    }

    [Fact]
    public void Duration_uses_korean_units()
    {
        Assert.Equal(
            "2시간 5분",
            UsageCardBuilder.Duration(TimeSpan.FromSeconds(7_500), UsageStrings.Korean, CultureInfo.InvariantCulture)
        );
    }
}
