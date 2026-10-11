// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using RunnerDuck.AIUsage;
using RunnerDuck.AIUsage.Presentation;
using RunnerDuck.App.Properties;

namespace RunnerDuck.App.Dashboard;

internal sealed class AIUsageCard : DashboardCard
{
    private IReadOnlyList<UsageDashboardRow> rows = [];
    private DateTimeOffset? lastUpdatedDate;
    private DateTimeOffset now;

    public bool HasRows => rows.Count > 0;

    public void SetRows(IReadOnlyList<UsageDashboardRow> rows, DateTimeOffset? lastUpdatedDate, DateTimeOffset now)
    {
        this.rows = rows;
        this.lastUpdatedDate = lastUpdatedDate;
        this.now = now;
        Invalidate();
    }

    public override int MeasureHeight(int width) => LayoutContent(null, width);

    protected override void PaintContent(Graphics graphics) => LayoutContent(graphics, Width);

    private int LayoutContent(Graphics? graphics, int width)
    {
        if (!HasRows)
        {
            return 0;
        }
        var primaryHeight = LineHeight(PrimaryFont);
        var secondaryHeight = LineHeight(SecondaryFont);
        var contentWidth = width - Padding12 * 2;
        var gap = LogicalToDeviceUnits(6);
        var detailX = Padding12 + LogicalToDeviceUnits(12);
        var labelWidth = LogicalToDeviceUnits(44);
        var remainingWidth = LogicalToDeviceUnits(40);
        var resetWidth = LogicalToDeviceUnits(72);
        var barHeight = LogicalToDeviceUnits(6);
        var resetX = width - Padding12 - resetWidth;
        var remainingX = resetX - gap - remainingWidth;
        var barX = detailX + labelWidth + gap;
        var barWidth = Math.Max(0, remainingX - gap - barX);
        var y = Padding12;
        if (graphics is not null)
        {
            DrawText(graphics, Strings.AIUsage, PrimaryFont, Theme.Text, new Rectangle(Padding12, y, contentWidth / 2, primaryHeight));
            if (lastUpdatedDate is { } date)
            {
                DrawText(graphics, RelativeTimeText.Format(date, now), SecondaryFont, Theme.SecondaryText,
                    new Rectangle(Padding12 + contentWidth / 2, y, contentWidth - contentWidth / 2, primaryHeight), TextFormatFlags.Right);
            }
        }
        y += primaryHeight;
        foreach (var row in rows)
        {
            y += Padding8;
            if (graphics is not null)
            {
                using var pen = new Pen(Theme.Divider);
                graphics.DrawLine(pen, Padding12, y, width - Padding12, y);
            }
            y += Padding8;
            if (graphics is not null)
            {
                var statusWidth = string.IsNullOrEmpty(row.StatusText) ? 0
                    : Math.Min(contentWidth / 2, TextRenderer.MeasureText(row.StatusText, SecondaryFont, Size.Empty, TextFormatFlags.NoPadding).Width);
                var titleWidth = statusWidth == 0 ? contentWidth : contentWidth - statusWidth - gap;
                DrawText(graphics, row.Title, PrimaryFont, Theme.Text, new Rectangle(Padding12, y, titleWidth, primaryHeight));
                if (statusWidth > 0)
                {
                    DrawText(graphics, row.StatusText, SecondaryFont,
                        row.Status == AIUsageStatus.SignInRequired ? Theme.Warning : Theme.Failure,
                        new Rectangle(width - Padding12 - statusWidth, y, statusWidth, primaryHeight), TextFormatFlags.Right);
                }
            }
            y += primaryHeight + LogicalToDeviceUnits(2);
            foreach (var window in row.Windows)
            {
                if (graphics is not null)
                {
                    DrawText(graphics, window.Label, SecondaryFont, Theme.SecondaryText, new Rectangle(detailX, y, labelWidth, secondaryHeight));
                    if (barWidth > 0)
                    {
                        DrawBar(graphics, new Rectangle(barX, y + (secondaryHeight - barHeight) / 2, barWidth, barHeight),
                            window.RemainingFraction, window.IsLow ? Theme.Failure : Theme.Accent);
                    }
                    DrawText(graphics, window.RemainingText, SecondaryFont, Theme.Text,
                        new Rectangle(remainingX, y, remainingWidth, secondaryHeight), TextFormatFlags.Right);
                    DrawText(graphics, window.ResetText, SecondaryFont, Theme.SecondaryText,
                        new Rectangle(resetX, y, resetWidth, secondaryHeight), TextFormatFlags.Right);
                }
                y += secondaryHeight;
            }
        }
        return y + Padding12;
    }
}
