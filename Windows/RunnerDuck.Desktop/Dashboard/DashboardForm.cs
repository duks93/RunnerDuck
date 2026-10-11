/*
 DashboardForm.cs
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

using System.Globalization;
using System.Runtime.InteropServices;
using RunnerDuck.AIUsage;
using RunnerDuck.AIUsage.Presentation;
using RunnerDuck.App.Properties;
using RunnerDuck.Core;

namespace RunnerDuck.App.Dashboard;

internal sealed partial class DashboardForm : Form
{
    private const int LogicalWidth = 300;
    private const int DropShadowClassStyle = 0x00020000;
    private const int ToolWindowExtendedStyle = 0x00000080;
    private const int WindowCornerPreferenceAttribute = 33;
    private const int RoundedCornerPreference = 2;

    private readonly Label titleLabel = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label menuButton = new()
    {
        AutoSize = false,
        Text = "",
        TextAlign = ContentAlignment.MiddleCenter,
        AccessibleName = Strings.Menu,
        AccessibleRole = AccessibleRole.PushButton,
    };
    private readonly SystemInfoCard systemInfoCard = new();
    private readonly List<CustomMetricsCard> customMetricsCards = [];
    private readonly AIUsageCard aiUsageCard = new();
    private readonly ContextMenuStrip menu;
    private DashboardTheme theme = DashboardTheme.Current();
    private int maximumHeight = int.MaxValue;
    private bool isAnchoredToBottom = true;

    public DashboardForm(string appName, ContextMenuStrip menu)
    {
        this.menu = menu;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        titleLabel.Text = appName;
        titleLabel.Font = new Font("Segoe UI", 9.75f);
        menuButton.Font = new Font("Segoe Fluent Icons", 10f);
        menuButton.MouseEnter += (_, _) => menuButton.BackColor = theme.CardBackground;
        menuButton.MouseLeave += (_, _) => menuButton.BackColor = theme.Background;
        menuButton.Click += (_, _) => menu.Show(menuButton, new Point(0, menuButton.Height));
        aiUsageCard.Visible = false;
        Controls.AddRange([titleLabel, menuButton, systemInfoCard, aiUsageCard]);
        KeyDown += (_, arguments) =>
        {
            if (arguments.KeyCode == Keys.Escape)
            {
                Hide();
            }
        };
    }

    public DateTimeOffset LastHiddenAt { get; private set; } = DateTimeOffset.MinValue;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= DropShadowClassStyle;
            parameters.ExStyle |= ToolWindowExtendedStyle;
            return parameters;
        }
    }

    private IEnumerable<DashboardCard> Cards => new DashboardCard[] { systemInfoCard }.Concat(customMetricsCards).Append(aiUsageCard);

    public void Render(Metrics metrics, IReadOnlyList<CustomMetricsBundle> customMetricsBundles, IReadOnlyList<AIUsageBundle>? aiUsageBundles, DateTimeOffset now)
    {
        systemInfoCard.SetRows(DashboardRows.Build(metrics));
        while (customMetricsCards.Count < customMetricsBundles.Count)
        {
            var card = new CustomMetricsCard();
            card.ApplyTheme(theme);
            customMetricsCards.Add(card);
            Controls.Add(card);
        }
        while (customMetricsCards.Count > customMetricsBundles.Count)
        {
            var card = customMetricsCards[^1];
            customMetricsCards.RemoveAt(customMetricsCards.Count - 1);
            Controls.Remove(card);
            card.Dispose();
        }
        for (var index = 0; index < customMetricsBundles.Count; index++)
        {
            customMetricsCards[index].SetBundle(customMetricsBundles[index], now);
        }
        aiUsageCard.SetRows(
            UsageDashboardRows.Build(aiUsageBundles ?? [], now, UsageStrings.For(CultureInfo.CurrentUICulture), CultureInfo.CurrentUICulture),
            UsageDashboardRows.LastUpdatedDate(aiUsageBundles ?? []),
            now);
        aiUsageCard.Visible = aiUsageCard.HasRows;
        if (Visible)
        {
            LayoutContent();
        }
    }

    public void ShowNear(Point anchor)
    {
        theme = DashboardTheme.Current();
        ApplyTheme();
        var workingArea = Screen.FromPoint(anchor).WorkingArea;
        var margin = LogicalToDeviceUnits(12);
        maximumHeight = workingArea.Height - margin * 2;
        isAnchoredToBottom = anchor.Y >= workingArea.Top + workingArea.Height / 2;
        LayoutContent();
        Left = Math.Clamp(anchor.X - Width / 2, workingArea.Left + margin, workingArea.Right - Width - margin);
        PlaceVertically(workingArea, margin);
        Show();
        Activate();
    }

    private void PlaceVertically(Rectangle workingArea, int margin) =>
        Top = isAnchoredToBottom
            ? Math.Max(workingArea.Top + margin, workingArea.Bottom - Height - margin)
            : workingArea.Top + margin;

    protected override void OnHandleCreated(EventArgs arguments)
    {
        base.OnHandleCreated(arguments);
        var preference = RoundedCornerPreference;
        DwmSetWindowAttribute(Handle, WindowCornerPreferenceAttribute, ref preference, sizeof(int));
    }

    protected override void OnDeactivate(EventArgs arguments)
    {
        base.OnDeactivate(arguments);
        if (!menu.Visible)
        {
            Hide();
            LastHiddenAt = DateTimeOffset.Now;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleLabel.Font.Dispose();
            menuButton.Font.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ApplyTheme()
    {
        BackColor = theme.Background;
        titleLabel.BackColor = theme.Background;
        titleLabel.ForeColor = theme.SecondaryText;
        menuButton.BackColor = theme.Background;
        menuButton.ForeColor = theme.Text;
        foreach (var card in Cards)
        {
            card.ApplyTheme(theme);
        }
    }

    private void LayoutContent()
    {
        var width = LogicalToDeviceUnits(LogicalWidth);
        var padding = LogicalToDeviceUnits(8);
        var headerHeight = LogicalToDeviceUnits(32);
        var contentWidth = width - padding * 2;
        AutoScrollPosition = Point.Empty;
        titleLabel.SetBounds(padding * 2, padding, contentWidth - headerHeight - padding, headerHeight);
        menuButton.SetBounds(width - padding - headerHeight, padding, headerHeight, headerHeight);
        var y = padding + headerHeight + padding;
        foreach (var card in Cards)
        {
            // Child Visible is false while the form is hidden, so use the card's content visibility.
            if (card == aiUsageCard && !aiUsageCard.HasRows)
            {
                continue;
            }
            var cardHeight = card.MeasureHeight(contentWidth);
            card.SetBounds(padding, y, contentWidth, cardHeight);
            y += cardHeight + padding;
        }
        AutoScroll = y > maximumHeight;
        AutoScrollMinSize = AutoScroll ? new Size(0, y) : Size.Empty;
        ClientSize = new Size(width + (AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0), Math.Min(y, maximumHeight));
        if (Visible)
        {
            PlaceVertically(Screen.FromControl(this).WorkingArea, LogicalToDeviceUnits(12));
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
