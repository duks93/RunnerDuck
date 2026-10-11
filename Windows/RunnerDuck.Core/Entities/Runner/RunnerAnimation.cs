// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunnerDuck.Core;

public enum RunnerMotionState { Idle, Walk, Run }

public sealed record RunnerAnimationFrame(int X, int Y, int Width, int Height);
public sealed record RunnerAnimationClip(int[] Frames, double Fps, bool Loop)
{
    [JsonIgnore] public double Duration => Frames.Length / Fps;
}

public sealed record RunnerAnimation(int Version, string Atlas, RunnerAnimationFrame[] Frames, Dictionary<string, RunnerAnimationClip> Clips)
{
    public static readonly string[] RequiredClips = ["idle", "walk", "run", "idle_to_walk", "walk_to_idle", "walk_to_run", "run_to_walk"];

    public static RunnerAnimation Parse(string json)
    {
        var animation = JsonSerializer.Deserialize<RunnerAnimation>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (animation is null || animation.Version != 1 || animation.Atlas != "duck-atlas.png"
            || animation.Frames is not { Length: > 0 and <= 256 } || animation.Clips is null
            || animation.Frames.Any(f => f is null || f.X < 0 || f.Y < 0 || f.Width <= 0 || f.Height <= 0)
            || RequiredClips.Any(name => !animation.Clips.TryGetValue(name, out var clip) || clip is null
                || !double.IsFinite(clip.Fps) || clip.Fps <= 0 || clip.Fps > 60
                || clip.Frames is not { Length: > 0 }
                || clip.Frames.Any(i => i < 0 || i >= animation.Frames.Length)
                || clip.Loop != !name.Contains("_to_", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The bundled duck animation is invalid.");
        }
        return animation;
    }
}
