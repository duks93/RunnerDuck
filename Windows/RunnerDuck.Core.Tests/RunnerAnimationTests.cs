using System.Text.Json;

namespace RunnerDuck.Core.Tests;

public sealed class RunnerAnimationTests
{
    private static RunnerAnimation Animation() => new(1, "duck-atlas.png",
        Enumerable.Range(0, 7).Select(i => new RunnerAnimationFrame(i * 32, 0, 32, 24)).ToArray(),
        RunnerAnimation.RequiredClips.ToDictionary(name => name, name => name.Contains("_to_")
            ? new RunnerAnimationClip([4, 5, 6], 10, false)
            : new RunnerAnimationClip([0, 1, 2, 3], 4, true)));

    [Fact]
    public void Default_walk_advances_before_first_cpu_sample_and_at_zero_load()
    {
        var player = new RunnerAnimationPlayer(Animation());
        Assert.Equal(RunnerMotionState.Walk, player.State);
        Assert.Equal(RunnerMotionState.Walk, player.Target);
        Assert.Equal("walk", player.ClipName);
        player.Advance(.4);
        Assert.Equal(1, player.FrameIndex);
        player.UpdateLoad(0, false, 0);
        player.Advance(.4);
        Assert.Equal(2, player.FrameIndex);
        Assert.Equal(RunnerMotionState.Walk, player.State);
    }

    [Theory]
    [InlineData(0, RunnerMotionState.Walk)]
    [InlineData(14.99, RunnerMotionState.Walk)]
    [InlineData(15, RunnerMotionState.Walk)]
    [InlineData(59.99, RunnerMotionState.Walk)]
    [InlineData(60, RunnerMotionState.Run)]
    [InlineData(100, RunnerMotionState.Run)]
    public void Initial_load_selects_expected_state(double load, RunnerMotionState expected)
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(load, false, 0);
        Assert.Equal(expected, player.Target);
    }

    [Fact]
    public void Hysteresis_prevents_flapping_between_walk_and_run()
    {
        var player = new RunnerAnimationPlayer(Animation());
        var loads = new[] { 15d, 14, 10.01, 10, 60, 59, 55.01, 55 };
        var states = new[] { RunnerMotionState.Walk, RunnerMotionState.Walk, RunnerMotionState.Walk,
            RunnerMotionState.Walk, RunnerMotionState.Run, RunnerMotionState.Run, RunnerMotionState.Run, RunnerMotionState.Walk };
        for (var i = 0; i < loads.Length; i++)
        {
            player.UpdateLoad(loads[i], false, i * 1000);
            Assert.Equal(states[i], player.Target);
        }
    }

    [Fact]
    public void Brief_spike_is_smoothed_and_reversed_option_changes_state()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(0, false, 0);
        player.UpdateLoad(100, false, .1);
        Assert.Equal(RunnerMotionState.Walk, player.Target);
        player.UpdateLoad(0, true, 1000);
        Assert.Equal(RunnerMotionState.Run, player.Target);
        player.UpdateLoad(100, true, 2000);
        Assert.Equal(RunnerMotionState.Walk, player.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Missing_or_invalid_cpu_keeps_walking_even_when_reversed(double? load)
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(load, true, 0);
        Assert.Equal(RunnerMotionState.Walk, player.Target);
    }

    [Fact]
    public void High_load_transitions_from_default_walk_to_run_in_300ms()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(100, false, 0);
        player.Advance(.15);
        Assert.Equal("walk_to_run", player.ClipName);
        Assert.Equal(5, player.FrameIndex);
        player.Advance(.16);
        Assert.Equal(RunnerMotionState.Run, player.State);
        Assert.Equal("run", player.ClipName);
        Assert.False(player.IsTransitioning);
    }

    [Fact]
    public void New_target_during_transition_waits_then_uses_latest_target()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(100, false, 0);
        player.Advance(.1);
        player.UpdateLoad(0, false, 1000);
        Assert.Equal("walk_to_run", player.ClipName);
        player.Advance(.21);
        Assert.Equal("run_to_walk", player.ClipName);
        player.Advance(.3);
        Assert.Equal(RunnerMotionState.Walk, player.State);
    }

    [Fact]
    public void Transition_waits_for_loop_connection_pose()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.Advance(.2);
        player.UpdateLoad(100, false, 0);
        player.Advance(.69);
        Assert.Equal("walk", player.ClipName);
        player.Advance(.02);
        Assert.Equal("walk_to_run", player.ClipName);
    }

    [Fact]
    public void Invalid_elapsed_and_long_suspend_never_escape_frame_bounds()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(100, false, 0);
        foreach (var elapsed in new[] { double.NaN, double.PositiveInfinity, -1, 0, 1000000, .033 })
        {
            player.Advance(elapsed);
            Assert.InRange(player.FrameIndex, 0, 6);
        }
    }

    [Fact]
    public void Manifest_roundtrips_and_rejects_missing_clips_bad_rects_and_frame_indices()
    {
        var animation = Animation();
        var parsed = RunnerAnimation.Parse(JsonSerializer.Serialize(animation));
        Assert.Equal(7, parsed.Clips.Count);
        animation.Clips.Remove("run_to_walk");
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation() with { Atlas = "../external.png" };
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation();
        animation.Clips["walk"] = new([999], 12, true);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation();
        animation.Frames[0] = new(-1, 0, 32, 24);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
    }
}
