using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FC_Revolution.UI.ViewModels;

public sealed class ThumbnailItem
{
    private const int ThumbWidth = 64;
    private const int ThumbHeight = 60;

    public long             Frame  { get; init; }
    public WriteableBitmap? Bitmap { get; init; }
    public string           Label  => $"帧 {Frame}";

    public static ThumbnailItem Create(long frame, uint[] thumb)
        => new ThumbnailItem { Frame = frame, Bitmap = CreateBitmap(thumb, ThumbWidth, ThumbHeight) };

    public static WriteableBitmap CreateBitmap(uint[] thumb, int width, int height)
    {
        var bmp = new WriteableBitmap(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        unsafe
        {
            using var locked = bmp.Lock();
            var dst = (uint*)locked.Address;
            for (var y = 0; y < height; y++)
            {
                var sourceY = Math.Min(ThumbHeight - 1, y * ThumbHeight / Math.Max(1, height));
                for (var x = 0; x < width; x++)
                {
                    var sourceX = Math.Min(ThumbWidth - 1, x * ThumbWidth / Math.Max(1, width));
                    var argb = thumb[sourceY * ThumbWidth + sourceX];
                    dst[y * width + x] = (argb & 0xFF00FF00) | ((argb & 0x00FF0000) >> 16) | ((argb & 0x000000FF) << 16);
                }
            }
        }

        return bmp;
    }
}
