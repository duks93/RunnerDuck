using System.Drawing.Imaging;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tests;

public sealed class DuckAssetsTests
{
    [Theory]
    [InlineData(RunnerKind.Duck)]
    [InlineData(RunnerKind.DuckFilled)]
    public void Bundled_runner_loads_every_animation_frame_and_walks(RunnerKind kind)
    {
        var frames = DuckAssets.LoadFrames(kind);
        try
        {
            Assert.Equal(DuckAssets.Animation.Frames.Length, frames.Count);
            Assert.Equal(8, frames.Count);
            Assert.Equal(Enumerable.Range(0, 8), DuckAssets.Animation.Clips["walk"].Frames);
            var body = frames[0].GetPixel(70, 70);
            if (kind == RunnerKind.DuckFilled)
            {
                Assert.True(body.A > 240 && body.R > 240 && body.G > 240 && body.B > 240);
            }
            else
            {
                Assert.True(body.A < 10); // The original antialiased ink has faint alpha inside the body.
            }
            var player = new RunnerAnimationPlayer(DuckAssets.Animation);
            var before = player.FrameIndex;
            player.Advance(0.25);
            Assert.NotEqual(before, player.FrameIndex);
            Assert.All(frames, frame => Assert.Equal(new System.Drawing.Size(192, 128), frame.Size));
            var atlasName = kind == RunnerKind.Duck ? DuckAssets.Animation.Atlas : "duck-filled-atlas.png";
            using var atlas = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Duck", atlasName));
            for (var index = 0; index < frames.Count; index++)
            {
                var rect = DuckAssets.Animation.Frames[index];
                using var expected = atlas.Clone(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), PixelFormat.Format32bppArgb);
                Assert.Equal(PngData(expected), PngData(frames[index]));
            }
            using var thumbnail = DuckAssets.LoadThumbnail(kind);
            var firstFrame = frames[DuckAssets.Animation.Clips["walk"].Frames[0]];
            Assert.Equal(firstFrame.Size, thumbnail.Size);
            Assert.Equal(PngData(firstFrame), PngData(thumbnail));
        }
        finally { foreach (var frame in frames) frame.Dispose(); }
    }

    private static byte[] PngData(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
