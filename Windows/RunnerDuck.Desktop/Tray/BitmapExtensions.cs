/*
 BitmapExtensions.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

// Adapted from RunCat365 (Apache License 2.0): https://github.com/runcat-dev/RunCat365

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RunnerDuck.App.Tray;

internal static class BitmapExtensions
{
    public static Bitmap Recolor(this Bitmap bitmap, Color color)
    {
        var result = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }
        var bounds = new Rectangle(0, 0, result.Width, result.Height);
        var data = result.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[Math.Abs(data.Stride) * result.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            for (var index = 0; index < pixels.Length; index += 4)
            {
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            result.UnlockBits(data);
        }
        return result;
    }

    public static Bitmap FitToSquare(this Image image, int size, bool flipsHorizontally, double scaleFactor = 1)
    {
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var scale = Math.Min((float)size / image.Width, (float)size / image.Height) * (float)scaleFactor;
        var width = image.Width * scale;
        var height = image.Height * scale;
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(image, new RectangleF((size - width) / 2, (size - height) / 2, width, height));
        }
        if (flipsHorizontally)
        {
            result.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }
        return result;
    }

    public static Bitmap FitToHeight(this Image image, int height)
    {
        var width = Math.Max(1, (int)Math.Round(image.Width * (double)height / image.Height));
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(image, new Rectangle(0, 0, width, height));
        return result;
    }

    public static Rectangle OpaqueBounds(IEnumerable<Bitmap> bitmaps)
    {
        var bounds = Rectangle.Empty;
        foreach (var bitmap in bitmaps)
        {
            var frameBounds = OpaqueBounds(bitmap);
            bounds = bounds.IsEmpty ? frameBounds : frameBounds.IsEmpty ? bounds : Rectangle.Union(bounds, frameBounds);
        }
        return bounds;
    }

    public static Bitmap Crop(this Bitmap bitmap, Rectangle bounds) =>
        bounds.IsEmpty || bounds.Size == bitmap.Size
            ? new Bitmap(bitmap)
            : bitmap.Clone(Rectangle.Intersect(bounds, new Rectangle(Point.Empty, bitmap.Size)), PixelFormat.Format32bppArgb);

    public static Icon ToIcon(this Bitmap bitmap)
    {
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        var pngData = pngStream.ToArray();
        var iconStream = new MemoryStream();
        using (var writer = new BinaryWriter(iconStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write((byte)(bitmap.Width >= 256 ? 0 : bitmap.Width));
            writer.Write((byte)(bitmap.Height >= 256 ? 0 : bitmap.Height));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(pngData.Length);
            writer.Write(22);
            writer.Write(pngData);
        }
        iconStream.Position = 0;
        return new Icon(iconStream);
    }

    private static Rectangle OpaqueBounds(Bitmap bitmap)
    {
        using var copy = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(copy))
        {
            graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }
        var data = copy.LockBits(new Rectangle(Point.Empty, copy.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var pixels = new byte[stride * copy.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            int left = copy.Width, top = copy.Height, right = -1, bottom = -1;
            for (var y = 0; y < copy.Height; y++)
            {
                for (var x = 0; x < copy.Width; x++)
                {
                    if (pixels[y * stride + x * 4 + 3] == 0)
                    {
                        continue;
                    }
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
            return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally
        {
            copy.UnlockBits(data);
        }
    }
}
