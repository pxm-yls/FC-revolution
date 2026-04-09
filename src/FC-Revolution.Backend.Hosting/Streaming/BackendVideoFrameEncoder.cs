using System.Runtime.InteropServices;
using FCRevolution.Backend.Hosting;
using SkiaSharp;

namespace FCRevolution.Backend.Hosting.Streaming;

internal sealed class BackendVideoFrameEncoder : IDisposable
{
    private const int SourceWidth = 256;
    private const int SourceHeight = 240;

    private readonly SKImageInfo _sourceInfo = new(SourceWidth, SourceHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
    private readonly uint[] _enhancedBuffer = new uint[SourceWidth * SourceHeight];
    private SKBitmap? _scaledBitmap;
    private int _cachedScale;

    internal BackendEncodedVideoFrame? Encode(uint[] argb, int scale, int quality, PixelEnhancementMode enhancement)
    {
        if (scale != _cachedScale)
        {
            _scaledBitmap?.Dispose();
            _scaledBitmap = scale > 1
                ? new SKBitmap(SourceWidth * scale, SourceHeight * scale, SKColorType.Bgra8888, SKAlphaType.Premul)
                : null;
            _cachedScale = scale;
        }

        var frameToEncode = argb;
        if (enhancement != PixelEnhancementMode.None)
        {
            PixelEnhancer.Apply(argb, _enhancedBuffer, SourceWidth, SourceHeight, enhancement);
            frameToEncode = _enhancedBuffer;
        }

        byte[]? jpeg;
        var handle = GCHandle.Alloc(frameToEncode, GCHandleType.Pinned);
        try
        {
            using var sourcePixmap = new SKPixmap(_sourceInfo, handle.AddrOfPinnedObject(), SourceWidth * 4);
            if (scale == 1)
            {
                using var data = sourcePixmap.Encode(SKEncodedImageFormat.Jpeg, quality);
                jpeg = data?.ToArray();
            }
            else
            {
                using var destinationPixmap = _scaledBitmap!.PeekPixels();
                sourcePixmap.ScalePixels(destinationPixmap, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                using var data = destinationPixmap.Encode(SKEncodedImageFormat.Jpeg, quality);
                jpeg = data?.ToArray();
            }
        }
        finally
        {
            handle.Free();
        }

        if (jpeg == null)
            return null;

        var outputWidth = SourceWidth * Math.Max(1, scale);
        var outputHeight = SourceHeight * Math.Max(1, scale);
        var metadata = ((outputWidth & 0xFFFF) << 16) | (outputHeight & 0xFFFF);
        return new BackendEncodedVideoFrame(jpeg, metadata);
    }

    public void Dispose()
    {
        _scaledBitmap?.Dispose();
        _scaledBitmap = null;
    }
}

internal readonly record struct BackendEncodedVideoFrame(byte[] Jpeg, int Metadata);
