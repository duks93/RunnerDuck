// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tray;

internal static class DuckAssets
{
    private static readonly ConcurrentDictionary<RunnerKind, Lazy<RunnerAnimation>> animations = new();

    // The duck variants share one manifest; each white runner ships its own manifest and atlas.
    public static RunnerAnimation Animation(RunnerKind kind) => animations.GetOrAdd(kind, key => new(() =>
    {
        var (directory, manifest, atlas) = Resource(key);
        return RunnerAnimation.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, directory, manifest)), atlas);
    })).Value;

    public static IReadOnlyList<Bitmap> LoadFrames(RunnerKind kind = RunnerKind.Duck)
    {
        var frames = new List<Bitmap>();
        try
        {
            using var atlas = LoadAtlas(kind);
            foreach (var rect in Animation(kind).Frames)
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
        var animation = Animation(kind);
        return LoadFrame(atlas, animation.Frames[animation.Clips["walk"].Frames[0]]);
    }

    public static string AtlasPath(RunnerKind kind)
    {
        var (directory, _, atlas) = Resource(kind);
        return Path.Combine(AppContext.BaseDirectory, directory, kind == RunnerKind.DuckFilled ? "duck-filled-atlas.png" : atlas);
    }

    private static (string Directory, string Manifest, string Atlas) Resource(RunnerKind kind) => kind switch
    {
        RunnerKind.Duck or RunnerKind.DuckFilled => ("Duck", "animation.json", "duck-atlas.png"),
        _ => ("Runners", $"{kind.ID()}.json", $"{kind.ID()}.png"),
    };

    private static Bitmap LoadAtlas(RunnerKind kind)
    {
        using var source = new Bitmap(AtlasPath(kind));
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
            throw new InvalidDataException("A runner animation frame is outside its atlas.");
        return atlas.Clone(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), PixelFormat.Format32bppArgb);
    }
}
