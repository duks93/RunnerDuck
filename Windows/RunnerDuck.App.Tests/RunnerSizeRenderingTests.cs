using RunnerDuck.App.Tray;
using RunnerDuck.Core;

namespace RunnerDuck.App.Tests;

public sealed class RunnerSizeRenderingTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void Larger_settings_increase_visible_pixels_within_the_fixed_tray_area(int iconSize)
    {
        using var source = new Bitmap(40, 40);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.FillRectangle(Brushes.Black, 8, 8, 24, 24);
        }
        var areas = new List<int>();
        foreach (var size in Enum.GetValues<RunnerSize>())
        {
            using var rendered = source.FitToSquare(iconSize, false, size.Scale());
            Assert.Equal(new Size(iconSize, iconSize), rendered.Size);
            var bounds = BitmapExtensions.OpaqueBounds([rendered]);
            areas.Add(bounds.Width * bounds.Height);
        }
        Assert.True(areas[0] < areas[1] && areas[1] < areas[2]);
    }

    [Theory]
    [InlineData(RunnerSize.Small)]
    [InlineData(RunnerSize.Standard)]
    [InlineData(RunnerSize.Large)]
    public void Size_changes_keep_horizontal_flip_and_center_the_image(RunnerSize size)
    {
        using var source = new Bitmap(40, 20);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.FillRectangle(Brushes.Red, 0, 0, 20, 20);
            graphics.FillRectangle(Brushes.Blue, 20, 0, 20, 20);
        }
        using var normal = source.FitToSquare(32, false, size.Scale());
        using var flipped = source.FitToSquare(32, true, size.Scale());
        for (var y = 0; y < normal.Height; y++)
        for (var x = 0; x < normal.Width; x++)
        {
            Assert.Equal(normal.GetPixel(x, y), flipped.GetPixel(31 - x, y));
        }
        var bounds = BitmapExtensions.OpaqueBounds([normal]);
        Assert.Equal(32, bounds.Top + bounds.Bottom);
        Assert.Equal(32, bounds.Left + bounds.Right);
    }
}
