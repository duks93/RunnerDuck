/*
 RunnerTrayIcon.cs
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

// Adapted from RunCat365 (Apache License 2.0): https://github.com/runcat-dev/RunCat365

namespace RunnerDuck.App.Tray;

internal sealed class RunnerTrayIcon : IDisposable
{
    internal const int MaximumToolTipLength = 127;

    private readonly NotifyIcon notifyIcon = new();
    private IReadOnlyList<Icon> icons = [];
    private int frameIndex;

    public RunnerTrayIcon(ContextMenuStrip contextMenu)
    {
        notifyIcon.ContextMenuStrip = contextMenu;
        notifyIcon.MouseClick += (_, arguments) =>
        {
            if (arguments.Button == MouseButtons.Left)
            {
                LeftClicked?.Invoke();
            }
        };
    }

    public event Action? LeftClicked;

    public void Show() => notifyIcon.Visible = true;

    public void SetFrames(IReadOnlyList<Bitmap> frames)
    {
        var previousIcons = icons;
        icons = frames.Select(frame => frame.ToIcon()).ToList();
        frameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, icons.Count - 1));
        notifyIcon.Icon = icons.Count > 0 ? icons[frameIndex] : null;
        DisposeIcons(previousIcons);
    }

    public void SelectFrame(int index)
    {
        if (index < 0 || index >= icons.Count || frameIndex == index) return;
        frameIndex = index;
        notifyIcon.Icon = icons[frameIndex];
    }

    public void AdvanceFrame()
    {
        if (icons.Count == 0)
        {
            return;
        }
        frameIndex = (frameIndex + 1) % icons.Count;
        notifyIcon.Icon = icons[frameIndex];
    }

    public void ShowBalloon(string title, string text) => notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning);

    public void SetToolTip(string text) =>
        notifyIcon.Text = text.Length > MaximumToolTipLength ? text[..MaximumToolTipLength] : text;

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        DisposeIcons(icons);
        icons = [];
    }

    private static void DisposeIcons(IReadOnlyList<Icon> iconsToDispose)
    {
        foreach (var icon in iconsToDispose)
        {
            icon.Dispose();
        }
    }
}
