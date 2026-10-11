/*
 UsageCardBuilder.cs
 RunnerDuck.AIUsage

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

using System.Globalization;
using System.Text.Json.Nodes;

namespace RunnerDuck.AIUsage.Presentation;

public static class UsageCardBuilder
{
    internal const int SmallRowLimit = 4;
    internal const int MediumLineBudget = 8;
    internal const int LargeLineBudget = 14;

    internal const double LowRemainingThreshold = 10;

    public static JsonObject Build(
        IReadOnlyList<AIUsageBundle>? bundles,
        DateTimeOffset? lastUpdatedDate,
        DateTimeOffset now,
        UsageStrings strings,
        CultureInfo culture
    )
    {
        var message = bundles switch
        {
            null => strings.Loading,
            [] => strings.Empty,
            _ => "",
        };
        var rows = bundles ?? [];
        var lastUpdated = lastUpdatedDate is { } date
            ? string.Format(culture, strings.LastUpdatedFormat, date.ToString("t", culture))
            : "";
        return new JsonObject
        {
            ["message"] = message,
            ["hasMessage"] = message.Length > 0,
            ["lastUpdated"] = lastUpdated,
            ["hasLastUpdated"] = lastUpdated.Length > 0 && rows.Count > 0,
            ["refreshTitle"] = strings.Refresh,
            ["smallRows"] = Rows(rows.Take(SmallRowLimit), bundle => bundle.SummaryWindows, now, strings, culture),
            ["mediumRows"] = Rows(
                WithinBudget(rows, MediumLineBudget, bundle => bundle.SummaryWindows),
                bundle => bundle.SummaryWindows,
                now,
                strings,
                culture
            ),
            ["largeRows"] = Rows(
                WithinBudget(rows, LargeLineBudget, bundle => bundle.Windows),
                bundle => bundle.Windows,
                now,
                strings,
                culture
            ),
        };
    }

    internal static IEnumerable<AIUsageBundle> WithinBudget(
        IReadOnlyList<AIUsageBundle> bundles,
        int lineBudget,
        Func<AIUsageBundle, IReadOnlyList<AIUsageWindow>> windows
    )
    {
        var usedLines = 0;
        foreach (var bundle in bundles)
        {
            var lines = 1 + windows(bundle).Count;
            if (usedLines > 0 && usedLines + lines > lineBudget)
            {
                yield break;
            }
            usedLines += lines;
            yield return bundle;
        }
    }

    internal static string Duration(TimeSpan interval, UsageStrings strings, CultureInfo culture)
    {
        if (interval <= TimeSpan.Zero)
        {
            return "";
        }
        if (interval.Days > 0)
        {
            return string.Format(culture, strings.DaysHoursFormat, interval.Days, interval.Hours);
        }
        if (interval.Hours > 0)
        {
            return string.Format(culture, strings.HoursMinutesFormat, interval.Hours, interval.Minutes);
        }
        return string.Format(culture, strings.MinutesFormat, Math.Max(1, interval.Minutes));
    }

    private static JsonArray Rows(
        IEnumerable<AIUsageBundle> bundles,
        Func<AIUsageBundle, IReadOnlyList<AIUsageWindow>> windows,
        DateTimeOffset now,
        UsageStrings strings,
        CultureInfo culture
    ) =>
        new(bundles.Select(bundle => (JsonNode)Row(bundle, windows(bundle), now, strings, culture)).ToArray());

    private static JsonObject Row(
        AIUsageBundle bundle,
        IReadOnlyList<AIUsageWindow> windows,
        DateTimeOffset now,
        UsageStrings strings,
        CultureInfo culture
    )
    {
        var isAvailable = bundle.Status == AIUsageStatus.Available;
        var status = bundle.Status switch
        {
            AIUsageStatus.SignInRequired => strings.SignInExpired,
            AIUsageStatus.Failed => strings.Failed,
            _ => "",
        };
        var compact = isAvailable
            ? string.Join(" · ", bundle.SummaryWindows.Select(window => $"{ShortTitle(window.Kind, strings)} {RemainingText(window)}"))
            : status;
        return new JsonObject
        {
            ["title"] = bundle.DisplayName,
            ["isAvailable"] = isAvailable,
            ["status"] = status,
            ["statusColor"] = StatusColor(bundle.Status),
            ["compact"] = compact,
            ["windows"] = new JsonArray(windows.Select(window => (JsonNode)Window(window, now, strings, culture)).ToArray()),
        };
    }

    private static JsonObject Window(AIUsageWindow window, DateTimeOffset now, UsageStrings strings, CultureInfo culture)
    {
        var remaining = RoundedRemaining(window);
        return new JsonObject
        {
            ["label"] = ShortTitle(window.Kind, strings),
            ["remainingText"] = RemainingText(window),
            ["remainingWeight"] = remaining,
            ["usedWeight"] = 100 - remaining,
            ["hasRemaining"] = remaining > 0,
            ["hasUsed"] = remaining < 100,
            ["barStyle"] = window.RemainingPercentage <= LowRemainingThreshold ? "attention" : "accent",
            ["resetText"] = window.ResetDate is { } resetDate ? Duration(resetDate - now, strings, culture) : "",
        };
    }

    internal static int RoundedRemaining(AIUsageWindow window) => (int)Math.Round(window.RemainingPercentage);

    internal static string RemainingText(AIUsageWindow window) => $"{RoundedRemaining(window)}%";

    internal static string ShortTitle(AIUsageWindowKind kind, UsageStrings strings) => kind.Type switch
    {
        AIUsageWindowKindType.FiveHour => strings.FiveHourShort,
        AIUsageWindowKindType.Weekly => strings.WeeklyShort,
        AIUsageWindowKindType.Monthly => strings.MonthlyShort,
        _ => kind.Model ?? "",
    };

    private static string StatusColor(AIUsageStatus status) => status switch
    {
        AIUsageStatus.SignInRequired => "warning",
        AIUsageStatus.Failed => "attention",
        _ => "default",
    };
}
