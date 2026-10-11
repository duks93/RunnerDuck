/*
 CustomMetricsCard.cs
 RunnerDuck.App

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

using RunnerDuck.App.Properties;
using RunnerDuck.Core;

namespace RunnerDuck.App.Dashboard;

internal sealed class CustomMetricsCard : DashboardCard
{
    private CustomMetricsBundle? bundle;
    private DateTimeOffset now = DateTimeOffset.Now;

    private int BarHeight => LogicalToDeviceUnits(6);

    private int Indent => LogicalToDeviceUnits(12);

    public void SetBundle(CustomMetricsBundle newBundle, DateTimeOffset displayedDate)
    {
        bundle = newBundle;
        now = displayedDate;
        Invalidate();
    }

    public override int MeasureHeight(int width) => LayoutContent(null, width);

    protected override void PaintContent(Graphics graphics) => LayoutContent(graphics, Width);

    private int LayoutContent(Graphics? graphics, int width)
    {
        if (bundle is null)
        {
            return 0;
        }
        var snapshot = bundle.Snapshot;
        var primaryHeight = LineHeight(PrimaryFont);
        var secondaryHeight = LineHeight(SecondaryFont);
        var contentWidth = width - Padding12 * 2;
        var detailX = Padding12 + Indent;
        var detailWidth = contentWidth - Indent;
        var y = Padding12;
        if (graphics is not null)
        {
            DrawText(graphics, snapshot.Title, PrimaryFont, Theme.Text, new Rectangle(Padding12, y, contentWidth, primaryHeight));
        }
        y += primaryHeight + LogicalToDeviceUnits(2);
        foreach (var metric in snapshot.Metrics)
        {
            if (graphics is not null)
            {
                DrawText(graphics, $"{metric.Title}: {metric.FormattedValue}", SecondaryFont, Theme.SecondaryText, new Rectangle(detailX, y, detailWidth, secondaryHeight));
            }
            y += secondaryHeight;
            if (metric.ClampedNormalizedValue is { } value)
            {
                y += LogicalToDeviceUnits(2);
                if (graphics is not null)
                {
                    DrawBar(graphics, new Rectangle(detailX, y, detailWidth, BarHeight), value, Theme.Accent);
                }
                y += BarHeight + LogicalToDeviceUnits(4);
            }
        }
        if (graphics is not null)
        {
            var detail = bundle.IsFailed ? Strings.Failed : RelativeTimeText.Format(snapshot.LastUpdatedDate, now);
            DrawText(
                graphics,
                string.Format(Strings.LastUpdated, detail),
                SecondaryFont,
                bundle.IsFailed ? Theme.Failure : Theme.SecondaryText,
                new Rectangle(detailX, y, detailWidth, secondaryHeight)
            );
        }
        y += secondaryHeight;
        return y + Padding12;
    }
}
