using FCRevolution.Backend.Hosting;

namespace FCRevolution.Backend.Hosting.Streaming;

internal sealed class BackendStreamSettingsStore
{
    private volatile int _scaleMultiplier;
    private volatile int _jpegQuality;
    private volatile int _enhancementModeInt;

    internal BackendStreamSettingsStore(BackendHostOptions options)
    {
        Update(options.StreamScaleMultiplier, options.StreamJpegQuality, options.StreamEnhancementMode);
    }

    internal int ScaleMultiplier => _scaleMultiplier;

    internal int JpegQuality => _jpegQuality;

    internal PixelEnhancementMode EnhancementMode => (PixelEnhancementMode)_enhancementModeInt;

    internal void Update(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)
    {
        _scaleMultiplier = Math.Clamp(scaleMultiplier, 1, 3);
        _jpegQuality = Math.Clamp(jpegQuality, 60, 95);
        _enhancementModeInt = (int)enhancementMode;
    }
}
