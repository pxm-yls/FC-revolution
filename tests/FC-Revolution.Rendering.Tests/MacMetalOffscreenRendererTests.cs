using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using FCRevolution.Core;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;
using FCRevolution.Rendering.Diagnostics;
using FCRevolution.Rendering.Metal;

namespace FC_Revolution.Rendering.Tests;

public sealed class MacMetalOffscreenRendererTests
{
    [SkippableFact]
    public void Render_MatchesReferenceRenderer_ForPriorityAndLeftEdgeMasks()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        byte[] patternTable = BuildPatternTable(
            (0, BuildSolidTile(1)),
            (1, BuildSolidTile(2)),
            (2, BuildSolidTile(3)));

        LayeredFrameData frameData = CreateLayeredFrameData(
            frameWidth: 24,
            frameHeight: 8,
            chrAtlas: LayeredFrameBuilder.BuildChrAtlas(patternTable),
            palette:
            [
                0xFF010101u, 0xFF101010u, 0xFF202020u, 0xFF303030u,
                0xFF040404u, 0xFF404040u, 0xFF505050u, 0xFF606060u,
                0xFF070707u, 0xFF707070u, 0xFF808080u, 0xFF909090u,
                0xFF0A0A0Au, 0xFFA0A0A0u, 0xFFB0B0B0u, 0xFFC0C0C0u,
                0xFF0D0D0Du, 0xFFD00000u, 0xFFE00000u, 0xFFF00000u,
                0xFF001100u, 0xFF002200u, 0xFF003300u, 0xFF004400u,
                0xFF000011u, 0xFF000022u, 0xFF000033u, 0xFF000044u,
                0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u
            ],
            backgroundTiles:
            [
                new BackgroundTileRenderItem(0, 0, 0, 0, 0, 8),
                new BackgroundTileRenderItem(8, 0, 1, 4, 0, 8),
                new BackgroundTileRenderItem(16, 0, 0, 0, 0, 8)
            ],
            sprites:
            [
                new SpriteRenderItem(0, 0, 2, 16, 0, 0, 0, 0),
                new SpriteRenderItem(8, 0, 2, 16, 0, 0, 1, 1),
                new SpriteRenderItem(16, 0, 2, 16, 0, 0, 0, 2)
            ],
            showBackground: true,
            showSprites: true,
            showBackgroundLeft8: false,
            showSpritesLeft8: false);

        uint[] reference = ReferenceFrameRenderer.Render(frameData);
        uint[] gpuFrame = MacMetalOffscreenRenderer.Render(frameData);
        float diff = PixelDiff.Compare(reference, gpuFrame, channelTolerance: 1);

        Assert.True(diff <= 0f, $"Offscreen Metal diff {diff:P2} exceeded 0%");
    }

    [SkippableFact]
    public void Render_Spatial_Request_ProducesRequestedOutputSize()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        byte[] patternTable = BuildPatternTable((0, BuildSolidTile(1)));
        LayeredFrameData frameData = CreateLayeredFrameData(
            frameWidth: 24,
            frameHeight: 8,
            chrAtlas: LayeredFrameBuilder.BuildChrAtlas(patternTable),
            palette:
            [
                0xFF010101u, 0xFF202020u, 0xFF303030u, 0xFF404040u,
                0xFF050505u, 0xFF505050u, 0xFF606060u, 0xFF707070u,
                0xFF090909u, 0xFF808080u, 0xFF909090u, 0xFFA0A0A0u,
                0xFF0D0D0Du, 0xFFB0B0B0u, 0xFFC0C0C0u, 0xFFD0D0D0u,
                0xFF111111u, 0xFFE0E0E0u, 0xFFF0F0F0u, 0xFFFFFFFFu,
                0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u,
                0xFF555555u, 0xFF666666u, 0xFF777777u, 0xFF888888u,
                0xFF999999u, 0xFFAAAAAAu, 0xFFBBBBBBu, 0xFFCCCCCCu
            ],
            backgroundTiles:
            [
                new BackgroundTileRenderItem(0, 0, 0, 0, 0, 8),
                new BackgroundTileRenderItem(8, 0, 0, 0, 0, 8),
                new BackgroundTileRenderItem(16, 0, 0, 0, 0, 8)
            ],
            sprites: [],
            showBackground: true,
            showSprites: false,
            showBackgroundLeft8: true,
            showSpritesLeft8: true);

        uint[] gpuFrame = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.Spatial, 48, 16);

        Assert.Equal(48 * 16, gpuFrame.Length);
        Assert.Contains(gpuFrame, pixel => pixel != 0u);
    }

    [SkippableFact]
    public void Render_CanSwitchBetweenNoneAndSpatialAcrossInvocations()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        byte[] patternTable = BuildPatternTable((0, BuildSolidTile(1)), (1, BuildSolidTile(2)));
        LayeredFrameData frameData = CreateLayeredFrameData(
            frameWidth: 16,
            frameHeight: 8,
            chrAtlas: LayeredFrameBuilder.BuildChrAtlas(patternTable),
            palette:
            [
                0xFF010101u, 0xFF202020u, 0xFF303030u, 0xFF404040u,
                0xFF111111u, 0xFF505050u, 0xFF606060u, 0xFF707070u,
                0xFF222222u, 0xFF808080u, 0xFF909090u, 0xFFA0A0A0u,
                0xFF333333u, 0xFFB0B0B0u, 0xFFC0C0C0u, 0xFFD0D0D0u,
                0xFF444444u, 0xFFE0E0E0u, 0xFFF0F0F0u, 0xFFFFFFFFu,
                0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u,
                0xFF555555u, 0xFF666666u, 0xFF777777u, 0xFF888888u,
                0xFF999999u, 0xFFAAAAAAu, 0xFFBBBBBBu, 0xFFCCCCCCu
            ],
            backgroundTiles:
            [
                new BackgroundTileRenderItem(0, 0, 0, 0, 0, 8),
                new BackgroundTileRenderItem(8, 0, 1, 0, 0, 8)
            ],
            sprites: [],
            showBackground: true,
            showSprites: false,
            showBackgroundLeft8: true,
            showSpritesLeft8: true);

        uint[] noneA = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.None, 16, 8);
        uint[] spatialA = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.Spatial, 32, 16);
        uint[] noneB = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.None, 16, 8);
        uint[] spatialB = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.Spatial, 32, 16);

        Assert.Equal(16 * 8, noneA.Length);
        Assert.Equal(16 * 8, noneB.Length);
        Assert.Equal(32 * 16, spatialA.Length);
        Assert.Equal(32 * 16, spatialB.Length);
        Assert.Equal(noneA, noneB);
        Assert.Contains(spatialA, pixel => pixel != 0u);
        Assert.Contains(spatialB, pixel => pixel != 0u);
    }

    [SkippableTheory]
    [InlineData("Super Mario Bros", 90, 0.01f)]
    [InlineData("冒险岛3", 90, 0.01f)]
    [InlineData("忍者神龟2", 120, 0.01f)]
    public void Render_MatchesReferenceRenderer_ForRepresentativeRoms(
        string romNameFragment,
        int frameIndex,
        float maxDiffRatio)
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        string? romPath = FindRomPath(romNameFragment);
        Skip.If(string.IsNullOrWhiteSpace(romPath), $"ROM containing '{romNameFragment}' not found");

        var nes = new NesConsole();
        nes.LoadRom(romPath!);

        var extractor = new RenderDataExtractor();
        for (int i = 0; i < frameIndex; i++)
            nes.RunFrame();

        FrameMetadata metadata = extractor.Extract(nes.Ppu.CaptureRenderStateSnapshot());
        LayeredFrameData frameData = LayeredFrameBuilder.Build(metadata);
        uint[] reference = ReferenceFrameRenderer.Render(frameData);
        uint[] gpuFrame = MacMetalOffscreenRenderer.Render(frameData);
        float diff = PixelDiff.Compare(reference, gpuFrame, channelTolerance: 1);

        Assert.True(diff <= maxDiffRatio, $"{romNameFragment} frame {frameIndex}: Metal diff {diff:P2} exceeds {maxDiffRatio:P2}");
    }

    [SkippableFact]
    public void Render_TemporalWithoutMotionTexture_MatchesNone()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        LayeredFrameData frameData = CreateLayeredFrameData(
            frameWidth: 16,
            frameHeight: 8,
            chrAtlas: LayeredFrameBuilder.BuildChrAtlas(BuildPatternTable((0, BuildSolidTile(1)))),
            palette:
            [
                0xFF010101u, 0xFF202020u, 0xFF303030u, 0xFF404040u,
                0xFF111111u, 0xFF505050u, 0xFF606060u, 0xFF707070u,
                0xFF222222u, 0xFF808080u, 0xFF909090u, 0xFFA0A0A0u,
                0xFF333333u, 0xFFB0B0B0u, 0xFFC0C0C0u, 0xFFD0D0D0u,
                0xFF444444u, 0xFFE0E0E0u, 0xFFF0F0F0u, 0xFFFFFFFFu,
                0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u,
                0xFF555555u, 0xFF666666u, 0xFF777777u, 0xFF888888u,
                0xFF999999u, 0xFFAAAAAAu, 0xFFBBBBBBu, 0xFFCCCCCCu
            ],
            backgroundTiles:
            [
                new BackgroundTileRenderItem(0, 0, 0, 0, 0, 8),
                new BackgroundTileRenderItem(8, 0, 0, 0, 0, 8)
            ],
            sprites: [],
            showBackground: true,
            showSprites: false,
            showBackgroundLeft8: true,
            showSpritesLeft8: true);

        uint[] none = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.None, 16, 8);
        uint[] temporal = MacMetalOffscreenRenderer.Render(frameData, MacUpscaleMode.Temporal, 16, 8);

        Assert.Equal(none, temporal);
    }

    [SkippableFact]
    public void Render_TemporalMotionTexture_WritesDeterministicVerificationMarker()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "Metal offscreen renderer requires macOS");
        Skip.IfNot(MacMetalOffscreenRenderer.IsSupported, MacMetalOffscreenRenderer.UnavailableReason ?? "Metal bridge unavailable");

        TemporalMotionTextureInput motionTexture = CreateMotionTextureInput(width: 16, height: 8);
        Skip.IfNot(
            TryCreateLayeredFrameDataWithMotionTexture(
                frameWidth: 16,
                frameHeight: 8,
                chrAtlas: LayeredFrameBuilder.BuildChrAtlas(BuildPatternTable((0, BuildSolidTile(1)))),
                palette:
                [
                    0xFF010101u, 0xFF202020u, 0xFF303030u, 0xFF404040u,
                    0xFF111111u, 0xFF505050u, 0xFF606060u, 0xFF707070u,
                    0xFF222222u, 0xFF808080u, 0xFF909090u, 0xFFA0A0A0u,
                    0xFF333333u, 0xFFB0B0B0u, 0xFFC0C0C0u, 0xFFD0D0D0u,
                    0xFF444444u, 0xFFE0E0E0u, 0xFFF0F0F0u, 0xFFFFFFFFu,
                    0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u,
                    0xFF555555u, 0xFF666666u, 0xFF777777u, 0xFF888888u,
                    0xFF999999u, 0xFFAAAAAAu, 0xFFBBBBBBu, 0xFFCCCCCCu
                ],
                backgroundTiles:
                [
                    new BackgroundTileRenderItem(0, 0, 0, 0, 0, 8),
                    new BackgroundTileRenderItem(8, 0, 0, 0, 0, 8)
                ],
                sprites: [],
                showBackground: true,
                showSprites: false,
                showBackgroundLeft8: true,
                showSpritesLeft8: true,
                motionTexture: motionTexture,
                frameData: out LayeredFrameData? frameData,
                expectedMotionBytes: out byte[]? expectedMotionBytes),
            "LayeredFrameData motion-texture payload is not available in this workspace shape");

        uint[] baseline = MacMetalOffscreenRenderer.Render(frameData!, MacUpscaleMode.None, 16, 8);
        uint[] temporal = MacMetalOffscreenRenderer.Render(frameData!, MacUpscaleMode.Temporal, 16, 8);

        Assert.Equal(baseline.Length, temporal.Length);
        Assert.Equal(baseline.AsSpan(0, baseline.Length - 1).ToArray(), temporal.AsSpan(0, temporal.Length - 1).ToArray());
        Assert.Equal(ComputeTemporalVerificationMarker(expectedMotionBytes!, 16, 8), temporal[^1]);
    }

    private static string? FindRomPath(string romNameFragment)
    {
        string repoRoot = GetRepositoryRoot();
        string romRoot = Path.Combine(repoRoot, "src", "FC-Revolution.UI", "bin");
        if (!Directory.Exists(romRoot))
            return null;

        return Directory.EnumerateFiles(romRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".nes", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(path => Path.GetFileName(path).Contains(romNameFragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var directory = new DirectoryInfo(current);
            if (directory.Exists && File.Exists(Path.Combine(directory.FullName, "FC-Revolution.slnx")))
                return directory.FullName;

            if (directory.Parent == null)
                break;

            current = directory.Parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static byte[] BuildPatternTable(params (int TileId, byte[] TileData)[] tiles)
    {
        var patternTable = new byte[0x2000];
        foreach (var (tileId, tileData) in tiles)
            Buffer.BlockCopy(tileData, 0, patternTable, tileId * 16, tileData.Length);

        return patternTable;
    }

    private static byte[] BuildSolidTile(byte colorIndex)
    {
        var tile = new byte[16];
        byte plane0 = (byte)((colorIndex & 0x01) != 0 ? 0xFF : 0x00);
        byte plane1 = (byte)((colorIndex & 0x02) != 0 ? 0xFF : 0x00);

        for (int row = 0; row < 8; row++)
        {
            tile[row] = plane0;
            tile[row + 8] = plane1;
        }

        return tile;
    }

    private static LayeredFrameData CreateLayeredFrameData(
        int frameWidth,
        int frameHeight,
        byte[] chrAtlas,
        uint[] palette,
        BackgroundTileRenderItem[] backgroundTiles,
        SpriteRenderItem[] sprites,
        bool showBackground,
        bool showSprites,
        bool showBackgroundLeft8,
        bool showSpritesLeft8)
    {
        if (!TryCreateLayeredFrameDataWithMotionTexture(
                frameWidth,
                frameHeight,
                chrAtlas,
                palette,
                backgroundTiles,
                sprites,
                showBackground,
                showSprites,
                showBackgroundLeft8,
                showSpritesLeft8,
                motionTexture: null,
                frameData: out LayeredFrameData? frameData,
                expectedMotionBytes: out _))
        {
            throw new InvalidOperationException("Unable to construct LayeredFrameData.");
        }

        return frameData!;
    }

    private static bool TryCreateLayeredFrameDataWithMotionTexture(
        int frameWidth,
        int frameHeight,
        byte[] chrAtlas,
        uint[] palette,
        BackgroundTileRenderItem[] backgroundTiles,
        SpriteRenderItem[] sprites,
        bool showBackground,
        bool showSprites,
        bool showBackgroundLeft8,
        bool showSpritesLeft8,
        TemporalMotionTextureInput? motionTexture,
        out LayeredFrameData? frameData,
        out byte[]? expectedMotionBytes)
    {
        expectedMotionBytes = null;

        foreach (ConstructorInfo constructor in typeof(LayeredFrameData).GetConstructors().OrderBy(candidate => candidate.GetParameters().Length))
        {
            if (!TryBuildLayeredFrameConstructorArguments(
                    constructor.GetParameters(),
                    frameWidth,
                    frameHeight,
                    chrAtlas,
                    palette,
                    backgroundTiles,
                    sprites,
                    showBackground,
                    showSprites,
                    showBackgroundLeft8,
                    showSpritesLeft8,
                    motionTexture,
                    out object?[]? arguments,
                    out bool consumedMotionTexture))
            {
                continue;
            }

            frameData = (LayeredFrameData)constructor.Invoke(arguments);
            if (motionTexture is null)
                return true;

            if (!(consumedMotionTexture || TryAttachMotionTexture(frameData, motionTexture.Value)))
                continue;

            if (!TryExtractMotionTextureBytes(frameData, out expectedMotionBytes))
                continue;

            return true;
        }

        frameData = null;
        expectedMotionBytes = null;
        return false;
    }

    private static bool TryBuildLayeredFrameConstructorArguments(
        ParameterInfo[] parameters,
        int frameWidth,
        int frameHeight,
        byte[] chrAtlas,
        uint[] palette,
        BackgroundTileRenderItem[] backgroundTiles,
        SpriteRenderItem[] sprites,
        bool showBackground,
        bool showSprites,
        bool showBackgroundLeft8,
        bool showSpritesLeft8,
        TemporalMotionTextureInput? motionTexture,
        out object?[]? arguments,
        out bool consumedMotionTexture)
    {
        arguments = new object?[parameters.Length];
        consumedMotionTexture = false;

        for (int index = 0; index < parameters.Length; index++)
        {
            ParameterInfo parameter = parameters[index];
            string name = parameter.Name ?? string.Empty;

            if (name.Equals("frameWidth", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = frameWidth;
                continue;
            }

            if (name.Equals("frameHeight", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = frameHeight;
                continue;
            }

            if (name.Equals("chrAtlas", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = chrAtlas;
                continue;
            }

            if (name.Equals("palette", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = palette;
                continue;
            }

            if (name.Equals("backgroundTiles", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = backgroundTiles;
                continue;
            }

            if (name.Equals("sprites", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = sprites;
                continue;
            }

            if (name.Equals("showBackground", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = showBackground;
                continue;
            }

            if (name.Equals("showSprites", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = showSprites;
                continue;
            }

            if (name.Equals("showBackgroundLeft8", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = showBackgroundLeft8;
                continue;
            }

            if (name.Equals("showSpritesLeft8", StringComparison.OrdinalIgnoreCase))
            {
                arguments[index] = showSpritesLeft8;
                continue;
            }

            if (motionTexture is not null &&
                IsMotionTextureMemberName(name) &&
                TryCreateCompatibleMotionTextureValue(parameter.ParameterType, motionTexture.Value, out object? motionTextureValue))
            {
                arguments[index] = motionTextureValue;
                consumedMotionTexture = true;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }

            if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            {
                arguments[index] = null;
                continue;
            }

            arguments = null;
            consumedMotionTexture = false;
            return false;
        }

        return true;
    }

    private static bool TryAttachMotionTexture(LayeredFrameData frameData, TemporalMotionTextureInput motionTexture)
    {
        foreach (PropertyInfo property in typeof(LayeredFrameData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || !IsMotionTextureMemberName(property.Name))
                continue;

            if (!TryCreateCompatibleMotionTextureValue(property.PropertyType, motionTexture, out object? value))
                continue;

            property.SetValue(frameData, value);
            return true;
        }

        foreach (FieldInfo field in typeof(LayeredFrameData).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!IsMotionTextureMemberName(field.Name))
                continue;

            if (!TryCreateCompatibleMotionTextureValue(field.FieldType, motionTexture, out object? value))
                continue;

            field.SetValue(frameData, value);
            return true;
        }

        return false;
    }

    private static bool TryCreateCompatibleMotionTextureValue(Type targetType, TemporalMotionTextureInput motionTexture, out object? value)
    {
        if (targetType == typeof(byte[]))
        {
            value = motionTexture.Bytes;
            return true;
        }

        if (targetType == typeof(ReadOnlyMemory<byte>))
        {
            value = new ReadOnlyMemory<byte>(motionTexture.Bytes);
            return true;
        }

        if (targetType == typeof(Memory<byte>))
        {
            value = new Memory<byte>(motionTexture.Bytes);
            return true;
        }

        if (targetType == typeof(ArraySegment<byte>))
        {
            value = new ArraySegment<byte>(motionTexture.Bytes);
            return true;
        }

        if (targetType == typeof(Vector2[]))
        {
            value = motionTexture.Vectors;
            return true;
        }

        if (targetType == typeof(ushort[]))
        {
            value = motionTexture.PackedVectors;
            return true;
        }

        if (targetType == typeof(short[]))
        {
            value = MemoryMarshal.Cast<byte, short>(motionTexture.Bytes.AsSpan()).ToArray();
            return true;
        }

        if (targetType == typeof(uint[]))
        {
            value = MemoryMarshal.Cast<byte, uint>(motionTexture.Bytes.AsSpan()).ToArray();
            return true;
        }

        if (targetType == typeof(int[]))
        {
            value = MemoryMarshal.Cast<byte, int>(motionTexture.Bytes.AsSpan()).ToArray();
            return true;
        }

        if (targetType == typeof(float[]))
        {
            value = MemoryMarshal.Cast<byte, float>(motionTexture.Bytes.AsSpan()).ToArray();
            return true;
        }

        if (targetType == typeof(Half[]))
        {
            var halfValues = new Half[motionTexture.Vectors.Length * 2];
            for (int index = 0; index < motionTexture.Vectors.Length; index++)
            {
                halfValues[index * 2] = (Half)motionTexture.Vectors[index].X;
                halfValues[(index * 2) + 1] = (Half)motionTexture.Vectors[index].Y;
            }

            value = halfValues;
            return true;
        }

        if (targetType == typeof(ReadOnlyMemory<Vector2>))
        {
            value = new ReadOnlyMemory<Vector2>(motionTexture.Vectors);
            return true;
        }

        if (targetType == typeof(Memory<Vector2>))
        {
            value = new Memory<Vector2>(motionTexture.Vectors);
            return true;
        }

        if (targetType.IsArray)
        {
            value = null;
            return false;
        }

        foreach (ConstructorInfo constructor in targetType.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            bool matched = true;

            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];
                string name = parameter.Name ?? string.Empty;

                if (name.Contains("width", StringComparison.OrdinalIgnoreCase))
                {
                    arguments[index] = motionTexture.Width;
                    continue;
                }

                if (name.Contains("height", StringComparison.OrdinalIgnoreCase))
                {
                    arguments[index] = motionTexture.Height;
                    continue;
                }

                if (IsMotionTextureMemberName(name) || IsMotionDataMemberName(name))
                {
                    if (!TryCreateCompatibleMotionTextureValue(parameter.ParameterType, motionTexture, out object? innerValue))
                    {
                        matched = false;
                        break;
                    }

                    arguments[index] = innerValue;
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    arguments[index] = parameter.DefaultValue;
                    continue;
                }

                matched = false;
                break;
            }

            if (!matched)
                continue;

            value = constructor.Invoke(arguments);
            return true;
        }

        object? instance = targetType.IsValueType
            ? Activator.CreateInstance(targetType)
            : Activator.CreateInstance(targetType, nonPublic: false);
        if (instance is null)
        {
            value = null;
            return false;
        }

        bool wroteMember = false;

        foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite)
                continue;

            if (property.Name.Contains("width", StringComparison.OrdinalIgnoreCase))
            {
                property.SetValue(instance, motionTexture.Width);
                wroteMember = true;
                continue;
            }

            if (property.Name.Contains("height", StringComparison.OrdinalIgnoreCase))
            {
                property.SetValue(instance, motionTexture.Height);
                wroteMember = true;
                continue;
            }

            if ((IsMotionTextureMemberName(property.Name) || IsMotionDataMemberName(property.Name)) &&
                TryCreateCompatibleMotionTextureValue(property.PropertyType, motionTexture, out object? propertyValue))
            {
                property.SetValue(instance, propertyValue);
                wroteMember = true;
            }
        }

        foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.Name.Contains("width", StringComparison.OrdinalIgnoreCase))
            {
                field.SetValue(instance, motionTexture.Width);
                wroteMember = true;
                continue;
            }

            if (field.Name.Contains("height", StringComparison.OrdinalIgnoreCase))
            {
                field.SetValue(instance, motionTexture.Height);
                wroteMember = true;
                continue;
            }

            if ((IsMotionTextureMemberName(field.Name) || IsMotionDataMemberName(field.Name)) &&
                TryCreateCompatibleMotionTextureValue(field.FieldType, motionTexture, out object? fieldValue))
            {
                field.SetValue(instance, fieldValue);
                wroteMember = true;
            }
        }

        value = wroteMember ? instance : null;
        return wroteMember;
    }

    private static TemporalMotionTextureInput CreateMotionTextureInput(int width, int height)
    {
        var vectors = new Vector2[width * height];
        var packedVectors = new ushort[width * height * 2];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int vectorIndex = (y * width) + x;
                Vector2 vector = new((x + 1) * 0.25f, (y + 1) * -0.5f);
                vectors[vectorIndex] = vector;
                packedVectors[vectorIndex * 2] = BitConverter.HalfToUInt16Bits((Half)vector.X);
                packedVectors[(vectorIndex * 2) + 1] = BitConverter.HalfToUInt16Bits((Half)vector.Y);
            }
        }

        return new TemporalMotionTextureInput(width, height, MemoryMarshal.AsBytes<ushort>(packedVectors.AsSpan()).ToArray(), packedVectors, vectors);
    }

    private static bool TryExtractMotionTextureBytes(LayeredFrameData frameData, out byte[]? motionTextureBytes)
    {
        object? motionTexture = typeof(LayeredFrameData).GetProperty("MotionTexture", BindingFlags.Instance | BindingFlags.Public)?.GetValue(frameData);
        if (motionTexture is null)
        {
            motionTextureBytes = null;
            return false;
        }

        PropertyInfo? packedVectorsProperty = motionTexture.GetType().GetProperty("PackedVectors", BindingFlags.Instance | BindingFlags.Public);
        if (packedVectorsProperty?.GetValue(motionTexture) is ushort[] packedVectors)
        {
            motionTextureBytes = MemoryMarshal.AsBytes<ushort>(packedVectors.AsSpan()).ToArray();
            return true;
        }

        motionTextureBytes = null;
        return false;
    }

    private static bool IsMotionTextureMemberName(string name) =>
        name.Contains("motion", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("temporal", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("texture", StringComparison.OrdinalIgnoreCase);

    private static bool IsMotionDataMemberName(string name) =>
        name.Contains("data", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("byte", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("buffer", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("pixel", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("texel", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("value", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("vector", StringComparison.OrdinalIgnoreCase);

    private static uint ComputeTemporalVerificationMarker(byte[] motionTextureBytes, int width, int height)
    {
        uint hash = 2166136261u;
        foreach (byte value in motionTextureBytes)
        {
            hash ^= value;
            hash *= 16777619u;
        }

        hash = HashUInt32(hash, (uint)width);
        hash = HashUInt32(hash, (uint)height);
        hash = (hash & 0x00FFFFFFu) | 0xFF000000u;
        return (hash & 0x00FFFFFFu) == 0 ? 0xFF010203u : hash;
    }

    private static uint HashUInt32(uint hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            hash ^= (byte)((value >> shift) & 0xFFu);
            hash *= 16777619u;
        }

        return hash;
    }

    private readonly record struct TemporalMotionTextureInput(int Width, int Height, byte[] Bytes, ushort[] PackedVectors, Vector2[] Vectors);
}
