// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace RunnerDuck.AIUsage.Presentation;

public sealed record UsageDashboardWindow(string Label, string RemainingText, double RemainingFraction, bool IsLow, string ResetText);

public sealed record UsageDashboardRow(
    string ID,
    string Title,
    AIUsageStatus Status,
    string StatusText,
    string Compact,
    IReadOnlyList<UsageDashboardWindow> Windows
)
{
    public bool Equals(UsageDashboardRow? other) =>
        other is not null
        && ID == other.ID
        && Title == other.Title
        && Status == other.Status
        && StatusText == other.StatusText
        && Compact == other.Compact
        && Windows.SequenceEqual(other.Windows);

    public override int GetHashCode() => HashCode.Combine(ID, Title, Status, StatusText, Compact, Windows.Count);
}

public static class UsageDashboardRows
{
    public static IReadOnlyList<UsageDashboardRow> Build(
        IReadOnlyList<AIUsageBundle> bundles,
        DateTimeOffset now,
        UsageStrings strings,
        CultureInfo culture
    ) => bundles.Select(bundle =>
    {
        var isAvailable = bundle.Status == AIUsageStatus.Available;
        var status = bundle.Status switch
        {
            AIUsageStatus.SignInRequired => strings.SignInExpired,
            AIUsageStatus.Failed => strings.Failed,
            _ => "",
        };
        var compact = isAvailable
            ? string.Join(" · ", bundle.SummaryWindows.Select(window => $"{UsageCardBuilder.ShortTitle(window.Kind, strings)} {UsageCardBuilder.RemainingText(window)}"))
            : status;
        return new UsageDashboardRow(
            bundle.ID,
            bundle.DisplayName,
            bundle.Status,
            status,
            compact,
            isAvailable
                ? bundle.Windows.Select(window => new UsageDashboardWindow(
                    UsageCardBuilder.ShortTitle(window.Kind, strings),
                    UsageCardBuilder.RemainingText(window),
                    Math.Clamp(window.RemainingPercentage / 100, 0, 1),
                    window.RemainingPercentage <= UsageCardBuilder.LowRemainingThreshold,
                    window.ResetDate is { } resetDate ? UsageCardBuilder.Duration(resetDate - now, strings, culture) : ""
                )).ToList()
                : []
        );
    }).ToList();

    public static DateTimeOffset? LastUpdatedDate(IReadOnlyList<AIUsageBundle> bundles) =>
        bundles.Count == 0 ? null : bundles.Min(bundle => bundle.LastUpdatedDate);
}
