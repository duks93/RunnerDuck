// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace RunnerDuck.Core;

public sealed class RunnerAnimationPlayer(RunnerAnimation animation)
{
    private double elapsed;
    private double? lastLoadTime;
    private double filteredLoad;
    public double PlaybackRate => 0.8 + 1.2 * filteredLoad / 100;
    public int FrameIndex
    {
        get
        {
            var clip = animation.Clips["walk"];
            return clip.Frames[Math.Min(clip.Frames.Length - 1, (int)(elapsed * clip.Fps))];
        }
    }

    public void UpdateLoad(double? percentage, bool reversed, double nowSeconds)
    {
        if (!double.IsFinite(nowSeconds)) return;
        var load = percentage is { } value && double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
        if (percentage is { } valid && double.IsFinite(valid) && reversed) load = 100 - load;
        var weight = lastLoadTime is { } previous ? 1 - Math.Exp(-Math.Max(0, nowSeconds - previous) / 2) : 1;
        filteredLoad += weight * (load - filteredLoad);
        lastLoadTime = nowSeconds;
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        elapsed = (elapsed + Math.Min(seconds, 10) * PlaybackRate) % animation.Clips["walk"].Duration;
    }
}
