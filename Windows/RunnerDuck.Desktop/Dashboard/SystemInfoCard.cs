/*
 SystemInfoCard.cs
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

internal sealed class SystemInfoCard : DashboardCard
{
    private IReadOnlyList<DashboardSystemRow> rows = [];

    private int GraphHeight => LogicalToDeviceUnits(20);

    private int BarHeight => LogicalToDeviceUnits(6);

    private int Indent => LogicalToDeviceUnits(12);

    public void SetRows(IReadOnlyList<DashboardSystemRow> newRows)
    {
        rows = newRows;
        Invalidate();
    }

    public override int MeasureHeight(int width) => LayoutRows(null, width);

    protected override void PaintContent(Graphics graphics) => LayoutRows(graphics, Width);

    private int LayoutRows(Graphics? graphics, int width)
    {
        var primaryHeight = LineHeight(PrimaryFont);
        var secondaryHeight = LineHeight(SecondaryFont);
        var contentWidth = width - Padding12 * 2;
        var detailX = Padding12 + Indent;
        var detailWidth = contentWidth - Indent;
        var y = Padding12;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (index > 0)
            {
                y += Padding8;
                if (graphics is not null)
                {
                    using var pen = new Pen(Theme.Divider);
                    graphics.DrawLine(pen, Padding12, y, width - Padding12, y);
                }
                y += Padding8;
            }
            if (graphics is not null)
            {
                DrawText(graphics, Summary(row), PrimaryFont, Theme.Text, new Rectangle(Padding12, y, contentWidth, primaryHeight));
            }
            y += primaryHeight + LogicalToDeviceUnits(2);
            foreach (var detail in row.Details)
            {
                if (graphics is not null)
                {
                    DrawText(graphics, Detail(detail), SecondaryFont, Theme.SecondaryText, new Rectangle(detailX, y, detailWidth, secondaryHeight));
                }
                y += secondaryHeight;
            }
            if (row.LineGraphValues.Count > 1)
            {
                y += LogicalToDeviceUnits(4);
                if (graphics is not null)
                {
                    DrawLineGraph(graphics, new Rectangle(detailX, y, detailWidth, GraphHeight), row.LineGraphValues);
                }
                y += GraphHeight;
            }
            if (row.BarGraphValue is { } barValue)
            {
                y += LogicalToDeviceUnits(4);
                if (graphics is not null)
                {
                    DrawBar(graphics, new Rectangle(detailX, y, detailWidth, BarHeight), barValue / 100, Theme.Accent);
                }
                y += BarHeight;
            }
        }
        return y + Padding12;
    }

    private void DrawLineGraph(Graphics graphics, Rectangle bounds, IReadOnlyList<double> values)
    {
        var points = new List<PointF> { new(bounds.Left, bounds.Bottom) };
        var step = (float)bounds.Width / (values.Count - 1);
        points.AddRange(values.Select((value, index) =>
            new PointF(bounds.Left + step * index, bounds.Bottom - (float)(bounds.Height * value / DashboardRows.MaximumGraphValue))));
        points.Add(new PointF(bounds.Right, bounds.Bottom));
        using var brush = new SolidBrush(Theme.Accent);
        graphics.FillPolygon(brush, points.ToArray());
    }

    private static string Summary(DashboardSystemRow row)
    {
        var (name, fallback) = row.Kind switch
        {
            SystemMetricKind.CPU => (Strings.CPU, ""),
            SystemMetricKind.Memory => (Strings.Memory, ""),
            SystemMetricKind.Storage => (Strings.Storage, ""),
            SystemMetricKind.Battery => (Strings.Battery, Strings.BatteryNotInstalled),
            SystemMetricKind.Network => (Strings.Network, Strings.NetworkDisconnected),
            _ => ("", ""),
        };
        var value = row.Value ?? (row.NetworkInterface is { } networkInterface ? NetworkName(networkInterface) : fallback);
        return $"{name}: {value}";
    }

    private static string NetworkName(NetworkInterface networkInterface) => networkInterface switch
    {
        NetworkInterface.WiFi => Strings.NetworkWiFi,
        NetworkInterface.Ethernet => Strings.NetworkEthernet,
        NetworkInterface.Cellular => Strings.NetworkCellular,
        _ => Strings.NetworkOther,
    };

    private static string Detail(DashboardDetail detail)
    {
        var label = detail.Kind switch
        {
            DashboardDetailKind.System => Strings.DetailSystem,
            DashboardDetailKind.User => Strings.DetailUser,
            DashboardDetailKind.Idle => Strings.DetailIdle,
            DashboardDetailKind.Used => Strings.DetailUsed,
            DashboardDetailKind.Available => Strings.DetailAvailable,
            DashboardDetailKind.Total => Strings.DetailTotal,
            DashboardDetailKind.Charging => Strings.DetailCharging,
            DashboardDetailKind.PluggedIn => Strings.DetailPluggedIn,
            DashboardDetailKind.OnBattery => Strings.DetailOnBattery,
            DashboardDetailKind.IPAddress => Strings.DetailIPAddress,
            DashboardDetailKind.Upload => Strings.DetailUpload,
            DashboardDetailKind.Download => Strings.DetailDownload,
            _ => "",
        };
        return detail.Value.Length == 0 ? label : $"{label}: {detail.Value}";
    }
}
