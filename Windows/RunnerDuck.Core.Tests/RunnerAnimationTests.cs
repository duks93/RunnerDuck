using System.Text.Json;

namespace RunnerDuck.Core.Tests;

public sealed class RunnerAnimationTests
{
    private static RunnerAnimation Animation() => new(1, "duck-atlas.png",
        Enumerable.Range(0, 8).Select(i => new RunnerAnimationFrame(i * 32, 0, 32, 24)).ToArray(),
        new() { ["walk"] = new(Enumerable.Range(0, 8).ToArray(), 8, true) });

    [Fact]
    public void Default_walk_advances_before_first_cpu_sample_and_at_zero_load()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.Advance(.4);
        Assert.Equal(2, player.FrameIndex);
        player.UpdateLoad(0, false, 0);
        player.Advance(.4);
        Assert.Equal(5, player.FrameIndex);
    }

    [Theory]
    [InlineData(-10, .8)]
    [InlineData(0, .8)]
    [InlineData(50, 1.4)]
    [InlineData(100, 2)]
    [InlineData(110, 2)]
    public void Load_changes_the_speed_of_the_same_walk(double load, double rate)
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(load, false, 0);
        Assert.Equal(rate, player.PlaybackRate, 10);
        player.Advance(.4);
        Assert.Equal((int)(.4 * rate * 8), player.FrameIndex);
    }

    [Fact]
    public void Brief_spike_is_smoothed_and_reversed_option_changes_speed()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(0, false, 0);
        player.UpdateLoad(100, false, .1);
        Assert.InRange(player.PlaybackRate, .8, .9);
        player.UpdateLoad(0, true, 1000);
        Assert.Equal(2, player.PlaybackRate);
        player.UpdateLoad(100, true, 2000);
        Assert.Equal(.8, player.PlaybackRate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Missing_or_invalid_cpu_keeps_walking_even_when_reversed(double? load)
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(load, true, 0);
        Assert.Equal(.8, player.PlaybackRate);
        player.Advance(.4);
        Assert.Equal(2, player.FrameIndex);
    }

    [Fact]
    public void Speed_changes_preserve_the_current_pose_and_continue_the_loop()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.Advance(.5);
        Assert.Equal(3, player.FrameIndex);
        player.UpdateLoad(100, false, 0);
        Assert.Equal(3, player.FrameIndex);
        player.Advance(.125);
        Assert.Equal(5, player.FrameIndex);
        player.Advance(.25);
        Assert.Equal(1, player.FrameIndex);
    }

    [Fact]
    public void Walk_wraps_after_the_last_frame()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.UpdateLoad(100, false, 0);
        var frames = new List<int>();
        for (var index = 0; index < 9; index++)
        {
            frames.Add(player.FrameIndex);
            player.Advance(1d / 16);
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0 }, frames);
    }

    [Fact]
    public void Invalid_time_is_ignored_and_long_suspend_stays_in_bounds()
    {
        var player = new RunnerAnimationPlayer(Animation());
        player.Advance(.4);
        foreach (var elapsed in new[] { double.NaN, double.PositiveInfinity, -1, 0 })
        {
            player.Advance(elapsed);
            Assert.Equal(2, player.FrameIndex);
        }
        player.UpdateLoad(100, false, double.NaN);
        Assert.Equal(.8, player.PlaybackRate);
        player.Advance(1000000);
        Assert.InRange(player.FrameIndex, 0, 7);
        player.Advance(.2);
        Assert.Equal(3, player.FrameIndex);
    }

    [Fact]
    public void Manifest_roundtrips_and_rejects_missing_or_extra_clips_and_bad_references()
    {
        var animation = Animation();
        var parsed = RunnerAnimation.Parse(JsonSerializer.Serialize(animation));
        Assert.Single(parsed.Clips);
        Assert.Equal(8, parsed.Frames.Length);
        animation.Clips.Remove("walk");
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation();
        animation.Clips["idle"] = new([0], 1, true);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation() with { Atlas = "../external.png" };
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation();
        animation.Clips["walk"] = new([999], 8, true);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
        animation = Animation();
        animation.Frames[0] = new(-1, 0, 32, 24);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(61, true)]
    [InlineData(8, false)]
    public void Manifest_rejects_invalid_timing_and_non_looping_walk(double fps, bool loop)
    {
        var animation = Animation();
        animation.Clips["walk"] = new([0, 1], fps, loop);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
    }

    [Fact]
    public void Manifest_rejects_an_empty_walk()
    {
        var animation = Animation();
        animation.Clips["walk"] = new([], 8, true);
        Assert.Throws<InvalidDataException>(() => RunnerAnimation.Parse(JsonSerializer.Serialize(animation)));
    }
}
