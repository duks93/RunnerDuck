using System.Drawing.Imaging;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tests;

public sealed class DuckAssetsTests
{
    public static TheoryData<RunnerKind> Kinds => [.. Enum.GetValues<RunnerKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Bundled_runner_loads_every_animation_frame_and_walks(RunnerKind kind)
    {
        var frames = DuckAssets.LoadFrames(kind);
        try
        {
            var animation = DuckAssets.Animation(kind);
            var count = kind.NumberOfResources();
            Assert.Equal(animation.Frames.Length, frames.Count);
            Assert.Equal(count, frames.Count);
            Assert.Equal(Enumerable.Range(0, count), animation.Clips["walk"].Frames);
            Assert.Equal(count, animation.Clips["walk"].Fps);
            if (kind == RunnerKind.Duck)
            {
                var body = frames[0].GetPixel(70, 70);
                Assert.True(body.A < 10); // The original antialiased ink has faint alpha inside the body.
            }
            else
            {
                Assert.True(WhitePixels(frames[0]) > 500, "A white runner lost its white body.");
                Assert.Equal(0, frames[0].GetPixel(0, 0).A);
            }
            var player = new RunnerAnimationPlayer(animation);
            var before = player.FrameIndex;
            player.Advance(0.25);
            Assert.NotEqual(before, player.FrameIndex);
            Assert.All(frames, frame => Assert.Equal(new System.Drawing.Size(192, 128), frame.Size));
            using var atlas = new Bitmap(DuckAssets.AtlasPath(kind));
            Assert.Equal(new System.Drawing.Size(count * 192, 128), atlas.Size);
            for (var index = 0; index < frames.Count; index++)
            {
                var rect = animation.Frames[index];
                using var expected = atlas.Clone(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), PixelFormat.Format32bppArgb);
                Assert.Equal(PngData(expected), PngData(frames[index]));
            }
            using var thumbnail = DuckAssets.LoadThumbnail(kind);
            var firstFrame = frames[animation.Clips["walk"].Frames[0]];
            Assert.Equal(firstFrame.Size, thumbnail.Size);
            Assert.Equal(PngData(firstFrame), PngData(thumbnail));
        }
        finally { foreach (var frame in frames) frame.Dispose(); }
    }

    [Fact]
    public void Filled_duck_keeps_the_duck_manifest_and_white_body()
    {
        var duck = DuckAssets.Animation(RunnerKind.Duck);
        var filled = DuckAssets.Animation(RunnerKind.DuckFilled);
        Assert.Equal(duck.Frames, filled.Frames);
        Assert.Equal(duck.Clips["walk"].Frames, filled.Clips["walk"].Frames);
        var frames = DuckAssets.LoadFrames(RunnerKind.DuckFilled);
        try
        {
            var body = frames[0].GetPixel(70, 70);
            Assert.True(body.A > 240 && body.R > 240 && body.G > 240 && body.B > 240);
        }
        finally { foreach (var frame in frames) frame.Dispose(); }
    }

    private static int WhitePixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 240 && pixel.R > 240 && pixel.G > 240 && pixel.B > 240) count++;
            }
        return count;
    }

    private static byte[] PngData(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
