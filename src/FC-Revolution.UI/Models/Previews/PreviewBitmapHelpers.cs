using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FC_Revolution.UI.Models.Previews;

internal static class PreviewBitmapHelpers
{
    public static WriteableBitmap CreateBitmap(int width, int height) =>
        new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

    public static unsafe void WriteBitmap(WriteableBitmap bitmap, byte[] pixels)
    {
        using var locked = bitmap.Lock();
        fixed (byte* src = pixels)
        {
            Buffer.MemoryCopy(
                src,
                (void*)locked.Address,
                (long)locked.RowBytes * bitmap.PixelSize.Height,
                pixels.Length);
        }
    }

    public static WriteableBitmap CreateBitmapFromBytes(int width, int height, byte[] pixels)
    {
        var bitmap = CreateBitmap(width, height);
        WriteBitmap(bitmap, pixels);
        return bitmap;
    }
}
