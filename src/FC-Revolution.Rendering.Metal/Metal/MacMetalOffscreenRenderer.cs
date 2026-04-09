using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Rendering.Metal;

public static class MacMetalOffscreenRenderer
{
    private static readonly string[] MotionTextureMemberNames =
    [
        "FullFrameMotionTexture",
        "MotionTexture",
        "TemporalMotionTexture",
        "MotionTexturePayload",
        "TemporalMotionTexturePayload"
    ];

    private static readonly string[] MotionTextureDataMemberNames =
    [
        "PackedVectors",
        "PackedBytes",
        "Data",
        "Bytes",
        "RawData",
        "Buffer",
        "Pixels",
        "Texels",
        "Values",
        "MotionVectors"
    ];

    private static readonly string[] WidthMemberNames = ["Width", "PixelWidth", "FrameWidth"];
    private static readonly string[] HeightMemberNames = ["Height", "PixelHeight", "FrameHeight"];

    private readonly record struct TemporalMotionTexturePayload(byte[] Bytes, int Width, int Height);

    public static bool IsSupported => MacMetalBridge.IsAvailable;

    public static string? UnavailableReason => MacMetalBridge.UnavailableReason;

    public static uint[] Render(LayeredFrameData frameData)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        return Render(frameData, MacUpscaleMode.None, frameData.FrameWidth, frameData.FrameHeight);
    }

    public static uint[] Render(
        LayeredFrameData frameData,
        MacUpscaleMode upscaleMode,
        int outputWidth,
        int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        if (outputWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputWidth));

        if (outputHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputHeight));

        var outputPixels = new uint[checked(outputWidth * outputHeight)];
        if (!TryRender(frameData, outputPixels, upscaleMode, outputWidth, outputHeight))
            throw new InvalidOperationException("离屏 Metal 渲染失败。");

        return outputPixels;
    }

    public static unsafe bool TryRender(LayeredFrameData frameData, uint[] outputPixels)
    {
        ArgumentNullException.ThrowIfNull(frameData);
        return TryRender(frameData, outputPixels, MacUpscaleMode.None, frameData.FrameWidth, frameData.FrameHeight);
    }

    public static unsafe bool TryRender(
        LayeredFrameData frameData,
        uint[] outputPixels,
        MacUpscaleMode upscaleMode,
        int outputWidth,
        int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(frameData);
        ArgumentNullException.ThrowIfNull(outputPixels);

        if (!OperatingSystem.IsMacOS() || !MacMetalBridge.IsAvailable)
            return false;

        if (outputWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputWidth));

        if (outputHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputHeight));

        int expectedPixelCount = checked(outputWidth * outputHeight);
        if (outputPixels.Length != expectedPixelCount)
            throw new ArgumentException($"Output pixel count must be {expectedPixelCount}.", nameof(outputPixels));

        TemporalMotionTexturePayload motionTexturePayload = default;
        bool hasTemporalMotionTexture = upscaleMode == MacUpscaleMode.Temporal &&
            TryGetTemporalMotionTexturePayload(frameData, out motionTexturePayload);
        byte[]? motionTextureBytes = hasTemporalMotionTexture ? motionTexturePayload.Bytes : null;

        fixed (byte* chrAtlas = frameData.ChrAtlas)
        fixed (uint* palette = frameData.Palette)
        fixed (BackgroundTileRenderItem* backgroundTiles = frameData.BackgroundTiles)
        fixed (SpriteRenderItem* sprites = frameData.Sprites)
        fixed (byte* motionTexture = motionTextureBytes)
        fixed (uint* output = outputPixels)
        {
            if (hasTemporalMotionTexture)
            {
                return MacMetalBridge.RenderLayeredFrameOffscreenExWithMotionTexture(
                    (IntPtr)chrAtlas,
                    (uint)frameData.ChrAtlas.Length,
                    (IntPtr)palette,
                    (uint)frameData.Palette.Length,
                    (IntPtr)backgroundTiles,
                    (uint)frameData.BackgroundTiles.Length,
                    (IntPtr)sprites,
                    (uint)frameData.Sprites.Length,
                    frameData.ShowBackground ? (byte)1 : (byte)0,
                    frameData.ShowSprites ? (byte)1 : (byte)0,
                    frameData.ShowBackgroundLeft8 ? (byte)1 : (byte)0,
                    frameData.ShowSpritesLeft8 ? (byte)1 : (byte)0,
                    (uint)frameData.FrameWidth,
                    (uint)frameData.FrameHeight,
                    upscaleMode,
                    (uint)outputWidth,
                    (uint)outputHeight,
                    (IntPtr)motionTexture,
                    (uint)motionTexturePayload.Bytes.Length,
                    (uint)motionTexturePayload.Width,
                    (uint)motionTexturePayload.Height,
                    (IntPtr)output,
                    (uint)outputPixels.Length);
            }

            return MacMetalBridge.RenderLayeredFrameOffscreenEx(
                (IntPtr)chrAtlas,
                (uint)frameData.ChrAtlas.Length,
                (IntPtr)palette,
                (uint)frameData.Palette.Length,
                (IntPtr)backgroundTiles,
                (uint)frameData.BackgroundTiles.Length,
                (IntPtr)sprites,
                (uint)frameData.Sprites.Length,
                frameData.ShowBackground ? (byte)1 : (byte)0,
                frameData.ShowSprites ? (byte)1 : (byte)0,
                frameData.ShowBackgroundLeft8 ? (byte)1 : (byte)0,
                frameData.ShowSpritesLeft8 ? (byte)1 : (byte)0,
                (uint)frameData.FrameWidth,
                (uint)frameData.FrameHeight,
                upscaleMode,
                (uint)outputWidth,
                (uint)outputHeight,
                (IntPtr)output,
                (uint)outputPixels.Length);
        }
    }

    private static bool TryGetTemporalMotionTexturePayload(LayeredFrameData frameData, out TemporalMotionTexturePayload payload)
    {
        payload = default;

        object? motionTexture = GetCandidateMemberValue(frameData, MotionTextureMemberNames);
        motionTexture ??= GetHeuristicMemberValue(
            frameData,
            static name => name.Contains("motion", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("texture", StringComparison.OrdinalIgnoreCase));
        if (motionTexture is null)
            return false;

        if (!TryGetMotionTextureBytes(motionTexture, out byte[] bytes))
        {
            object? motionTextureData = GetCandidateMemberValue(motionTexture, MotionTextureDataMemberNames);
            motionTextureData ??= GetHeuristicMemberValue(
                motionTexture,
                static name => name.Contains("data", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("byte", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("buffer", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("pixel", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("texel", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("value", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("vector", StringComparison.OrdinalIgnoreCase));
            if (motionTextureData is null || !TryGetMotionTextureBytes(motionTextureData, out bytes))
                return false;
        }

        int width = TryGetIntMemberValue(motionTexture, WidthMemberNames, out int resolvedWidth)
            ? resolvedWidth
            : frameData.FrameWidth;
        int height = TryGetIntMemberValue(motionTexture, HeightMemberNames, out int resolvedHeight)
            ? resolvedHeight
            : frameData.FrameHeight;
        if (bytes.Length == 0 || width <= 0 || height <= 0)
            return false;

        payload = new TemporalMotionTexturePayload(bytes, width, height);
        return true;
    }

    private static bool TryGetMotionTextureBytes(object value, out byte[] bytes)
    {
        switch (value)
        {
            case byte[] rawBytes when rawBytes.Length > 0:
                bytes = rawBytes;
                return true;
            case ReadOnlyMemory<byte> readOnlyMemory when !readOnlyMemory.IsEmpty:
                bytes = readOnlyMemory.ToArray();
                return true;
            case Memory<byte> memory when !memory.IsEmpty:
                bytes = memory.ToArray();
                return true;
            case ArraySegment<byte> segment when segment.Count > 0:
                bytes = segment.ToArray();
                return true;
            case Vector2[] vectors when vectors.Length > 0:
                bytes = MemoryMarshal.AsBytes<Vector2>(vectors.AsSpan()).ToArray();
                return true;
            case ReadOnlyMemory<Vector2> readOnlyVectors when !readOnlyVectors.IsEmpty:
                bytes = MemoryMarshal.AsBytes(readOnlyVectors.Span).ToArray();
                return true;
            case Memory<Vector2> vectorMemory when !vectorMemory.IsEmpty:
                bytes = MemoryMarshal.AsBytes(vectorMemory.Span).ToArray();
                return true;
            case Half[] halfValues when halfValues.Length > 0:
                bytes = MemoryMarshal.AsBytes<Half>(halfValues.AsSpan()).ToArray();
                return true;
            case Array array when TryCopyPrimitiveArrayBytes(array, out byte[] copiedBytes):
                bytes = copiedBytes;
                return true;
            default:
                bytes = Array.Empty<byte>();
                return false;
        }
    }

    private static bool TryCopyPrimitiveArrayBytes(Array array, out byte[] bytes)
    {
        try
        {
            int byteLength = Buffer.ByteLength(array);
            if (byteLength <= 0)
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            bytes = new byte[byteLength];
            Buffer.BlockCopy(array, 0, bytes, 0, byteLength);
            return true;
        }
        catch (ArgumentException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static bool TryGetIntMemberValue(object source, string[] memberNames, out int value)
    {
        object? rawValue = GetCandidateMemberValue(source, memberNames);
        if (rawValue is null)
        {
            value = 0;
            return false;
        }

        try
        {
            value = Convert.ToInt32(rawValue);
            return true;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static object? GetCandidateMemberValue(object source, string[] memberNames)
    {
        Type type = source.GetType();
        foreach (string memberName in memberNames)
        {
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(source);
                }
                catch
                {
                }
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field is not null)
                return field.GetValue(source);
        }

        return null;
    }

    private static object? GetHeuristicMemberValue(object source, Predicate<string> nameMatcher)
    {
        Type type = source.GetType();

        PropertyInfo? property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => candidate.GetIndexParameters().Length == 0 && nameMatcher(candidate.Name));
        if (property is not null)
        {
            try
            {
                return property.GetValue(source);
            }
            catch
            {
            }
        }

        FieldInfo? field = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => nameMatcher(candidate.Name));
        return field?.GetValue(source);
    }
}
