// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Drawing.Text;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tray;

internal sealed class MetricsTrayIcons(
    ContextMenuStrip menu,
    Action leftClicked,
    Func<MetricsBarItem, string> title,
    bool visible = true) : IDisposable
{
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<string, Entry> cachedEntries = CreateBuiltInEntries(menu, leftClicked);

    internal IReadOnlyDictionary<string, Entry> Entries => entries;
    internal IReadOnlyDictionary<string, Entry> CachedEntries => cachedEntries;

    private static Dictionary<string, Entry> CreateBuiltInEntries(ContextMenuStrip menu, Action leftClicked)
    {
        // Preserve this order across launches, regardless of which readings arrive first.
        string[] keys = [nameof(MetricsBarItemKind.CPU), nameof(MetricsBarItemKind.Memory),
            nameof(MetricsBarItemKind.Storage), nameof(MetricsBarItemKind.Battery), nameof(MetricsBarItemKind.Network)];
        return keys.ToDictionary(key => key, _ => new Entry(menu, leftClicked));
    }

    public void Update(IReadOnlyList<MetricsBarItem> items, int size, Color color)
    {
        var keys = items.Select(item => item.Key).ToHashSet();
        foreach (var key in entries.Keys.Where(key => !keys.Contains(key)).ToArray())
        {
            entries[key].Hide();
            entries.Remove(key);
        }
        foreach (var item in items)
        {
            if (!entries.TryGetValue(item.Key, out var entry))
            {
                if (!cachedEntries.TryGetValue(item.Key, out entry))
                {
                    entry = new Entry(menu, leftClicked);
                    cachedEntries.Add(item.Key, entry);
                }
                entries.Add(item.Key, entry);
            }
            entry.Update(item, title(item), size, color, visible);
        }
    }

    public void Dispose()
    {
        foreach (var entry in cachedEntries.Values) entry.Dispose();
        cachedEntries.Clear();
        entries.Clear();
    }

    internal sealed class Entry : IDisposable
    {
        private readonly NotifyIcon notifyIcon;
        private (string Text, int Size, Color Color)? rendered;
        internal Icon? Icon { get; private set; }
        internal string ToolTip => notifyIcon.Text;

        public Entry(ContextMenuStrip menu, Action leftClicked)
        {
            notifyIcon = new NotifyIcon { ContextMenuStrip = menu };
            notifyIcon.MouseClick += (_, arguments) =>
            {
                if (arguments.Button == MouseButtons.Left) leftClicked();
            };
        }

        public void Update(MetricsBarItem item, string title, int size, Color color, bool visible)
        {
            var presentation = (item.IconText, size, color);
            if (rendered != presentation)
            {
                using var bitmap = Render(item.IconText, size, color);
                var previous = Icon;
                Icon = bitmap.ToIcon();
                notifyIcon.Icon = Icon;
                previous?.Dispose();
                rendered = presentation;
            }
            var text = string.IsNullOrEmpty(item.DetailText) ? title : $"{title}: {item.DetailText}";
            notifyIcon.Text = text.Length > 127 ? text[..127] : text;
            notifyIcon.Visible = visible;
        }

        public void Hide()
        {
            notifyIcon.Visible = false;
            notifyIcon.Icon = null;
            Icon?.Dispose();
            Icon = null;
            rendered = null;
        }

        internal static Bitmap Render(string text, int size, Color color)
        {
            var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            using var baseFont = new Font("Segoe UI", size * 0.72f, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = graphics.MeasureString(text, baseFont, PointF.Empty, format);
            var scale = Math.Min(1, (size - 1f) / Math.Max(1, measured.Width));
            using var fittedFont = new Font(baseFont.FontFamily, baseFont.Size * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            graphics.DrawString(text, fittedFont, brush, new RectangleF(0, 0, size, size), format);
            return bitmap;
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Icon?.Dispose();
            Icon = null;
        }
    }
}
