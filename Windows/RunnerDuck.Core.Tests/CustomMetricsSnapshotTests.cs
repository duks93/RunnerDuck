using System.Text;

namespace RunnerDuck.Core.Tests;

public sealed class CustomMetricsSnapshotTests
{
    [Fact]
    public void Decode_reads_schema_fields()
    {
        var snapshot = CustomMetricsSnapshot.Decode(Encoding.UTF8.GetBytes("""
            {
              "title": "Claude Code",
              "symbol": "staroflife",
              "metricsBarValue": "5.4%",
              "metrics": [
                { "title": "Model", "formattedValue": "Opus 4.7" },
                { "title": "Context", "formattedValue": "5.4%", "normalizedValue": 0.054 }
              ],
              "lastUpdatedDate": "2026-06-05T04:50:40Z"
            }
            """));
        Assert.Equal(
            new CustomMetricsSnapshot
            {
                Title = "Claude Code",
                Symbol = "staroflife",
                MetricsBarValue = "5.4%",
                Metrics =
                [
                    new() { Title = "Model", FormattedValue = "Opus 4.7" },
                    new() { Title = "Context", FormattedValue = "5.4%", NormalizedValue = 0.054 },
                ],
                LastUpdatedDate = new DateTimeOffset(2026, 6, 5, 4, 50, 40, TimeSpan.Zero),
            },
            snapshot
        );
    }

    [Fact]
    public void Decode_accepts_empty_metrics_and_missing_optional_fields()
    {
        var snapshot = CustomMetricsSnapshot.Decode(Encoding.UTF8.GetBytes("""
            { "title": "Empty", "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z" }
            """));
        Assert.Null(snapshot.Symbol);
        Assert.Null(snapshot.MetricsBarValue);
        Assert.Empty(snapshot.Metrics);
    }

    [Theory]
    [InlineData("""{ "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "No metrics", "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "No date", "metrics": [] }""")]
    [InlineData("""{ "title": "Bad date", "metrics": [], "lastUpdatedDate": "yesterday" }""")]
    [InlineData("""{ "title": "Bad row", "metrics": [ { "title": "Row" } ], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Null row", "metrics": [ null ], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Comment", "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z" } // note""")]
    [InlineData("""{ "title": "Trailing", "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z", }""")]
    [InlineData("""{ "title": 42, "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": null, "metrics": [], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Null metrics", "metrics": null, "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Null date", "metrics": [], "lastUpdatedDate": null }""")]
    [InlineData("""{ "title": "Null value", "metrics": [ { "title": "Row", "formattedValue": null } ], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Number value", "metrics": [ { "title": "Row", "formattedValue": 5 } ], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("""{ "title": "Text ratio", "metrics": [ { "title": "Row", "formattedValue": "5", "normalizedValue": "0.5" } ], "lastUpdatedDate": "2026-06-05T04:50:40Z" }""")]
    [InlineData("not json")]
    public void Decode_rejects_invalid_snapshots(string json)
    {
        var error = Assert.Throws<CustomMetricsException>(() => CustomMetricsSnapshot.Decode(Encoding.UTF8.GetBytes(json)));
        Assert.Equal(CustomMetricsError.InvalidFormat, error.Error);
    }

    [Fact]
    public void Decode_accepts_null_optional_fields_and_clamps_parsed_ratios()
    {
        var snapshot = CustomMetricsSnapshot.Decode(Encoding.UTF8.GetBytes("""
            {
              "title": "Optional",
              "symbol": null,
              "metricsBarValue": null,
              "metrics": [
                { "title": "Low", "formattedValue": "-", "normalizedValue": -0.2 },
                { "title": "High", "formattedValue": "+", "normalizedValue": 1.7 },
                { "title": "None", "formattedValue": "?", "normalizedValue": null }
              ],
              "lastUpdatedDate": "2026-06-05T04:50:40+09:00"
            }
            """));
        Assert.Null(snapshot.Symbol);
        Assert.Null(snapshot.MetricsBarValue);
        Assert.Equal(new double?[] { 0.0, 1.0, null }, snapshot.Metrics.Select(metric => metric.ClampedNormalizedValue));
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 19, 50, 40, TimeSpan.Zero), snapshot.LastUpdatedDate);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(1.5, 1.0)]
    public void ClampedNormalizedValue_limits_value_to_unit_range(double? value, double? expected)
    {
        Assert.Equal(expected, new CustomMetric { Title = "Row", FormattedValue = "x", NormalizedValue = value }.ClampedNormalizedValue);
    }

    [Fact]
    public void MetricsBarLabel_shows_value_or_placeholder()
    {
        var snapshot = new CustomMetricsSnapshot { Title = "T", MetricsBarValue = "42", Metrics = [], LastUpdatedDate = FakeCustomMetricsFiles.Now };
        Assert.Equal("42", new CustomMetricsBundle(Guid.NewGuid(), snapshot).MetricsBarLabel);
        Assert.Equal("---", new CustomMetricsBundle(Guid.NewGuid(), snapshot, IsFailed: true).MetricsBarLabel);
        Assert.Equal("---", new CustomMetricsBundle(Guid.NewGuid(), snapshot with { MetricsBarValue = null }).MetricsBarLabel);
    }
}
