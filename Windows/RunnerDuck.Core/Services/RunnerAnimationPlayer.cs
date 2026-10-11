// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace RunnerDuck.Core;

public sealed class RunnerAnimationPlayer(RunnerAnimation animation)
{
    private double elapsed;
    private double? lastLoadTime;
    private double filteredLoad;
    private RunnerMotionState? transitionTarget;

    public RunnerMotionState State { get; private set; } = RunnerMotionState.Walk;
    public RunnerMotionState Target { get; private set; } = RunnerMotionState.Walk;
    public string ClipName { get; private set; } = "walk";
    public bool IsTransitioning => transitionTarget is not null;
    public int FrameIndex
    {
        get
        {
            var clip = animation.Clips[ClipName];
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
        Target = filteredLoad >= 60 || (Target == RunnerMotionState.Run && filteredLoad > 55)
            ? RunnerMotionState.Run
            : RunnerMotionState.Walk;
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        var remaining = Math.Min(seconds, 10);
        while (remaining > 0.0000001)
        {
            if (!IsTransitioning && elapsed < 0.0000001 && State != Target) BeginTransition();
            var clip = animation.Clips[ClipName];
            var rate = IsTransitioning ? 1 : PlaybackRate();
            var untilBoundary = (clip.Duration - elapsed) / rate;
            if (remaining < untilBoundary)
            {
                elapsed += remaining * rate;
                break;
            }
            remaining -= untilBoundary;
            elapsed = 0;
            if (transitionTarget is { } next)
            {
                State = next;
                transitionTarget = null;
                ClipName = State.ToString().ToLowerInvariant();
            }
        }
    }

    private void BeginTransition()
    {
        var next = State switch
        {
            RunnerMotionState.Idle or RunnerMotionState.Run => RunnerMotionState.Walk,
            _ => Target,
        };
        ClipName = $"{State.ToString().ToLowerInvariant()}_to_{next.ToString().ToLowerInvariant()}";
        transitionTarget = next;
    }

    private double PlaybackRate() => State switch
    {
        RunnerMotionState.Walk => 0.8 + 0.4 * Math.Clamp((filteredLoad - 10) / 50, 0, 1),
        RunnerMotionState.Run => 0.8 + 0.4 * Math.Clamp((filteredLoad - 55) / 45, 0, 1),
        _ => 1,
    };
}
