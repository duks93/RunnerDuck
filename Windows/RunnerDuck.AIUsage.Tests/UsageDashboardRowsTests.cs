using System.Globalization;
using RunnerDuck.AIUsage.Presentation;

namespace RunnerDuck.AIUsage.Tests;

public sealed class UsageDashboardRowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<UsageDashboardRow> Build(IReadOnlyList<AIUsageBundle> bundles, string language = "en-US")
    {
        var culture = CultureInfo.GetCultureInfo(language);
        return UsageDashboardRows.Build(bundles, Now, UsageStrings.For(culture), culture);
    }

    private static AIUsageBundle Bundle() => new(
        AIUsageProvider.ClaudeCode,
        AIUsageStatus.Available,
        [
            new(AIUsageWindowKind.WeeklyModel("Opus"), 10),
            new(AIUsageWindowKind.Weekly, 95.2),
            new(AIUsageWindowKind.FiveHour, 42.4, Now.AddHours(2).AddMinutes(5)),
            new(AIUsageWindowKind.Monthly, 25),
        ],
        Now,
        "account-1",
        "Work"
    );

    [Fact]
    public void Build_with_no_bundles_returns_no_rows()
    {
        Assert.Empty(Build([]));
    }

    [Theory]
    [InlineData("en-US", "Wk", "5h", "Mo", "2h 5m", "5h 58% · Wk 5%")]
    [InlineData("ko-KR", "주간", "5시간", "월간", "2시간 5분", "5시간 58% · 주간 5%")]
    public void Build_maps_all_windows_in_input_order_and_formats_summary(
        string language, string weekly, string fiveHour, string monthly, string reset, string compact)
    {
        var bundle = Bundle();
        var row = Assert.Single(Build([bundle], language));
        Assert.Equal(bundle.ID, row.ID);
        Assert.Equal("Claude Code · Work", row.Title);
        Assert.Equal(AIUsageStatus.Available, row.Status);
        Assert.Equal("", row.StatusText);
        Assert.Equal(compact, row.Compact);
        Assert.Equal(new[] { "Opus", weekly, fiveHour, monthly }, row.Windows.Select(window => window.Label));
        Assert.Equal(new[] { "90%", "5%", "58%", "75%" }, row.Windows.Select(window => window.RemainingText));
        Assert.Equal(0.576, row.Windows[2].RemainingFraction, 10);
        Assert.False(row.Windows[2].IsLow);
        Assert.True(row.Windows[1].IsLow);
        Assert.Equal(reset, row.Windows[2].ResetText);
        Assert.Equal("", row.Windows[0].ResetText);
    }

    [Theory]
    [InlineData(90, "10%", 0.1, true)]
    [InlineData(89.9, "10%", 0.101, false)]
    [InlineData(42.5, "58%", 0.575, false)]
    [InlineData(43.5, "56%", 0.565, false)]
    [InlineData(110, "-10%", 0, true)]
    [InlineData(-10, "110%", 1, false)]
    public void Build_rounds_text_clamps_fraction_and_uses_unrounded_low_threshold(
        double percentage, string text, double fraction, bool isLow)
    {
        var bundle = Bundle() with { Windows = [new(AIUsageWindowKind.FiveHour, percentage)] };
        var window = Assert.Single(Assert.Single(Build([bundle])).Windows);
        Assert.Equal(text, window.RemainingText);
        Assert.Equal(fraction, window.RemainingFraction, 10);
        Assert.Equal(isLow, window.IsLow);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    public void Build_with_elapsed_reset_returns_empty_reset_text(int seconds)
    {
        var bundle = Bundle() with { Windows = [new(AIUsageWindowKind.FiveHour, 0, Now.AddSeconds(seconds))] };
        Assert.Equal("", Assert.Single(Assert.Single(Build([bundle])).Windows).ResetText);
    }

    [Theory]
    [InlineData(AIUsageStatus.SignInRequired, "en-US", "Sign-in expired")]
    [InlineData(AIUsageStatus.Failed, "en-US", "Failed")]
    [InlineData(AIUsageStatus.SignInRequired, "ko-KR", "로그인 만료")]
    [InlineData(AIUsageStatus.Failed, "ko-KR", "실패")]
    public void Build_with_unavailable_status_localizes_text_and_hides_windows(
        AIUsageStatus status, string language, string expected)
    {
        var row = Assert.Single(Build([Bundle() with { Status = status }], language));
        Assert.Equal(status, row.Status);
        Assert.Equal(expected, row.StatusText);
        Assert.Equal(expected, row.Compact);
        Assert.Empty(row.Windows);
    }

    [Fact]
    public void Build_preserves_bundle_input_order()
    {
        var bundles = new[]
        {
            Bundle() with { AccountID = "z", AccountName = "Last", Status = AIUsageStatus.Failed },
            Bundle() with { AccountID = "a", AccountName = "First" },
            Bundle() with { AccountID = "m", Status = AIUsageStatus.SignInRequired },
        };
        var rows = Build(bundles);
        Assert.Equal(bundles.Select(bundle => bundle.ID), rows.Select(row => row.ID));
        Assert.Equal(bundles.Select(bundle => bundle.DisplayName), rows.Select(row => row.Title));
        Assert.Equal(bundles.Select(bundle => bundle.Status), rows.Select(row => row.Status));
    }

    [Fact]
    public void Build_uses_monthly_summary_when_weekly_is_absent()
    {
        var bundle = Bundle() with
        {
            Windows = [new(AIUsageWindowKind.Monthly, 25), new(AIUsageWindowKind.FiveHour, 42.4)],
        };
        Assert.Equal("5h 58% · Mo 75%", Assert.Single(Build([bundle])).Compact);
    }

    [Fact]
    public void Build_with_no_windows_returns_empty_compact_and_windows()
    {
        var row = Assert.Single(Build([Bundle() with { Windows = [] }]));
        Assert.Equal("", row.Compact);
        Assert.Equal("", row.StatusText);
        Assert.Empty(row.Windows);
    }

    [Fact]
    public void LastUpdatedDate_returns_minimum_across_all_statuses()
    {
        Assert.Equal(Now.AddMinutes(-10), UsageDashboardRows.LastUpdatedDate([
            Bundle(),
            Bundle() with { LastUpdatedDate = Now.AddMinutes(-10), Status = AIUsageStatus.Failed },
            Bundle() with { LastUpdatedDate = Now.AddMinutes(-5), Status = AIUsageStatus.SignInRequired },
        ]));
    }

    [Fact]
    public void LastUpdatedDate_with_no_bundles_returns_null()
    {
        Assert.Null(UsageDashboardRows.LastUpdatedDate([]));
    }

    [Fact]
    public void Row_equality_compares_window_values_and_order()
    {
        var row = Assert.Single(Build([Bundle()]));
        var equal = row with { Windows = row.Windows.Select(window => window with { }).ToList() };
        Assert.Equal(row, equal);
        Assert.Equal(row.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(row, row with { Windows = row.Windows.Reverse().ToList() });
        Assert.NotEqual(row, row with { Windows = [row.Windows[0] with { RemainingText = "1%" }] });
        Assert.NotEqual(row, row with { ID = "other" });
        Assert.NotEqual(row, row with { Title = "Other" });
        Assert.NotEqual(row, row with { Status = AIUsageStatus.Failed });
        Assert.NotEqual(row, row with { StatusText = "Failed" });
        Assert.NotEqual(row, row with { Compact = "Other" });
        Assert.False(row.Equals(null));
    }
}
