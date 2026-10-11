/*
 DashboardCard.cs
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

using System.Drawing.Drawing2D;

namespace RunnerDuck.App.Dashboard;

internal abstract class DashboardCard : Control
{
    private const int CornerRadius = 8;

    protected DashboardCard()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true
        );
    }

    protected DashboardTheme Theme { get; private set; } = DashboardTheme.Current();

    protected Font PrimaryFont { get; } = new("Segoe UI", 9.75f);

    protected Font SecondaryFont { get; } = new("Segoe UI", 9f);

    protected int Padding8 => LogicalToDeviceUnits(8);

    protected int Padding12 => LogicalToDeviceUnits(12);

    public void ApplyTheme(DashboardTheme theme)
    {
        Theme = theme;
        Invalidate();
    }

    public abstract int MeasureHeight(int width);

    protected abstract void PaintContent(Graphics graphics);

    protected override void OnPaint(PaintEventArgs arguments)
    {
        var graphics = arguments.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? Theme.Background);
        using (var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), LogicalToDeviceUnits(CornerRadius)))
        using (var brush = new SolidBrush(Theme.CardBackground))
        {
            graphics.FillPath(brush, path);
        }
        PaintContent(graphics);
    }

    protected int LineHeight(Font font) =>
        TextRenderer.MeasureText("Ag", font, Size.Empty, TextFormatFlags.NoPadding).Height + LogicalToDeviceUnits(3);

    protected void DrawText(Graphics graphics, string text, Font font, Color color, Rectangle bounds, TextFormatFlags alignment = TextFormatFlags.Left) =>
        TextRenderer.DrawText(
            graphics,
            text,
            font,
            bounds,
            color,
            alignment | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter
        );

    protected void DrawBar(Graphics graphics, Rectangle bounds, double fraction, Color color)
    {
        var radius = bounds.Height / 2;
        using (var path = RoundedRectangle(bounds, radius))
        using (var brush = new SolidBrush(Theme.Track))
        {
            graphics.FillPath(brush, path);
        }
        var width = (int)Math.Round(bounds.Width * Math.Clamp(fraction, 0, 1));
        if (width <= 0)
        {
            return;
        }
        using (var path = RoundedRectangle(new Rectangle(bounds.X, bounds.Y, width, bounds.Height), Math.Min(radius, width / 2)))
        using (var brush = new SolidBrush(color))
        {
            graphics.FillPath(brush, path);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PrimaryFont.Dispose();
            SecondaryFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
