namespace FCRevolution.Rendering.Abstractions;

public enum FrameMirroringMode
{
    Horizontal,
    Vertical,
    SingleLower,
    SingleUpper,
    FourScreen
}

public readonly record struct BackgroundScanlineRenderState
{
    public int FineScrollX { get; init; }

    public int FineScrollY { get; init; }

    public int CoarseScrollX { get; init; }

    public int CoarseScrollY { get; init; }

    public int NametableSelect { get; init; }

    public bool UseBackgroundPatternTableHighBank { get; init; }

    public bool ShowBackground { get; init; }

    public bool ShowBackgroundLeft8 { get; init; }
}

public sealed class RenderStateSnapshot
{
    public required byte[] NametableBytes { get; init; }

    public required byte[] PatternTableBytes { get; init; }

    public required uint[] PaletteColors { get; init; }

    public required byte[] OamBytes { get; init; }

    public required FrameMirroringMode MirroringMode { get; init; }

    public required int FineScrollX { get; init; }

    public required int FineScrollY { get; init; }

    public required int CoarseScrollX { get; init; }

    public required int CoarseScrollY { get; init; }

    public required int NametableSelect { get; init; }

    public required bool UseBackgroundPatternTableHighBank { get; init; }

    public required bool UseSpritePatternTableHighBank { get; init; }

    public required bool Use8x16Sprites { get; init; }

    public required bool ShowBackground { get; init; }

    public required bool ShowSprites { get; init; }

    public required bool ShowBackgroundLeft8 { get; init; }

    public required bool ShowSpritesLeft8 { get; init; }

    public required bool HasCapturedBackgroundScanlineStates { get; init; }

    public required BackgroundScanlineRenderState[] BackgroundScanlineStates { get; init; }
}
