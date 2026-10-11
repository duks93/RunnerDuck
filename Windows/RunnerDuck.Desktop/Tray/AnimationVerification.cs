// Copyright 2026 duks93. SPDX-License-Identifier: Apache-2.0
using System.Drawing.Imaging;
using System.Text.Json;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tray;

internal static class AnimationVerification
{
    // Uses the same loader, crop, scale, recolor, flip, and icon encoder as the tray.
    public static void Render(string outputDirectory)
    {
        RenderRunner(outputDirectory, RunnerKind.Duck);
        RenderRunner(Path.Combine(outputDirectory, "duck-filled"), RunnerKind.DuckFilled);
    }

    private static void RenderRunner(string outputDirectory, RunnerKind kind)
    {
        Directory.CreateDirectory(outputDirectory);
        var animation = DuckAssets.Animation;
        var frames = DuckAssets.LoadFrames(kind);
        try
        {
            var bounds = BitmapExtensions.OpaqueBounds(frames);
            foreach (var (name, clip) in animation.Clips)
            {
                using var sheet = new Bitmap(clip.Frames.Length * 40, 8 * 40);
                using var graphics = Graphics.FromImage(sheet);
                for (var row = 0; row < 8; row++)
                {
                    var dark = row >= 4;
                    var size = new[] { 16, 20, 24, 32 }[row % 4];
                    using var background = new SolidBrush(dark ? Color.FromArgb(30, 30, 30) : Color.White);
                    graphics.FillRectangle(background, 0, row * 40, sheet.Width, 40);
                    for (var i = 0; i < clip.Frames.Length; i++)
                    {
                        using var cropped = frames[clip.Frames[i]].Crop(bounds);
                        using var fitted = cropped.FitToSquare(size, flipsHorizontally: dark);
                        using var tinted = kind.IsTemplate() ? fitted.Recolor(dark ? Color.White : Color.Black) : new Bitmap(fitted);
                        using var icon = tinted.ToIcon();
                        graphics.DrawIcon(icon, new Rectangle(i * 40 + (40 - size) / 2, row * 40 + (40 - size) / 2, size, size));
                    }
                }
                sheet.Save(Path.Combine(outputDirectory, name + ".png"), ImageFormat.Png);
            }
            foreach (var name in new[] { "NOTICE.txt", "LICENSE.txt", "THIRD_PARTY.txt" })
                if (new FileInfo(Path.Combine(AppContext.BaseDirectory, "Legal", name)).Length == 0)
                    throw new InvalidDataException("A bundled license is empty.");
            var player = new RunnerAnimationPlayer(animation);
            for (var tick = 0; tick < 1800; tick++)
            {
                if (tick % 30 == 0) player.UpdateLoad(tick % 300 < 150 ? 100 : 0, tick > 900, tick / 30d);
                player.Advance(1 / 30d);
                if (player.FrameIndex < 0 || player.FrameIndex >= frames.Count) throw new InvalidDataException("Invalid frame.");
            }
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new
            {
                platform = "Windows", runner = kind.ID(), frames = frames.Count, clips = animation.Clips.Count,
                renderSizes = new[] { 16, 20, 24, 32 }, themeAndFlip = true, playbackSeconds = 60, licenses = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { foreach (var frame in frames) frame.Dispose(); }
    }
}
