// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tray;

internal static class DuckAssets
{
    private static readonly Lazy<RunnerAnimation> animation = new(() =>
        RunnerAnimation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Duck", "animation.json"))));
    public static RunnerAnimation Animation => animation.Value;

    public static IReadOnlyList<Bitmap> LoadFrames(RunnerKind kind = RunnerKind.Duck)
    {
        var frames = new List<Bitmap>();
        try
        {
            using var atlas = LoadAtlas(kind);
            foreach (var rect in Animation.Frames)
            {
                frames.Add(LoadFrame(atlas, rect));
            }
            return frames;
        }
        catch
        {
            foreach (var frame in frames) frame.Dispose();
            throw;
        }
    }

    public static Bitmap LoadThumbnail(RunnerKind kind = RunnerKind.Duck)
    {
        using var atlas = LoadAtlas(kind);
        return LoadFrame(atlas, Animation.Frames[Animation.Clips["walk"].Frames[0]]);
    }

    private static Bitmap LoadAtlas(RunnerKind kind)
    {
        var atlasName = kind switch
        {
            RunnerKind.Duck => Animation.Atlas,
            RunnerKind.DuckFilled => "duck-filled-atlas.png",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        using var source = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Duck", atlasName));
        var bounds = new Rectangle(Point.Empty, source.Size);
        var data = source.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            return CopyDecodedPixels(data);
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private static Bitmap CopyDecodedPixels(BitmapData source)
    {
        // Own the decoded pixels so frames remain valid after the PNG is unlocked and disposed.
        var decoded = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        try
        {
            var target = decoded.LockBits(new Rectangle(Point.Empty, decoded.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[source.Width * 4];
                for (var y = 0; y < source.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(source.Scan0, y * source.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(target.Scan0, y * target.Stride), row.Length);
                }
            }
            finally
            {
                decoded.UnlockBits(target);
            }
            return decoded;
        }
        catch
        {
            decoded.Dispose();
            throw;
        }
    }

    private static Bitmap LoadFrame(Bitmap atlas, RunnerAnimationFrame rect)
    {
        if (rect.X + (long)rect.Width > atlas.Width || rect.Y + (long)rect.Height > atlas.Height)
            throw new InvalidDataException("A duck animation frame is outside its atlas.");
        return atlas.Clone(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), PixelFormat.Format32bppArgb);
    }
}
