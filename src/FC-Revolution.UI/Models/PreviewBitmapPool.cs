using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FC_Revolution.UI.Models;

public static class PreviewBitmapPool
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<(int Width, int Height), Queue<WriteableBitmap>> Pool = new();
    private const int MaxPerBucket = 3;

    public static WriteableBitmap Rent(int width, int height)
    {
        lock (SyncRoot)
        {
            if (Pool.TryGetValue((width, height), out var queue) && queue.Count > 0)
                return queue.Dequeue();
        }

        return new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
    }

    public static void Return(WriteableBitmap bitmap)
    {
        var key = (bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        lock (SyncRoot)
        {
            if (!Pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<WriteableBitmap>();
                Pool[key] = queue;
            }

            if (queue.Count < MaxPerBucket)
                queue.Enqueue(bitmap);
        }
    }
}
