using FCRevolution.Core.Mappers;

namespace FCRevolution.Core.PPU;

public sealed class PpuRenderStateSnapshot
{
    public required byte[] NametableBytes { get; init; }

    public required byte[] PatternTableBytes { get; init; }

    public required uint[] PaletteColors { get; init; }

    public required byte[] OamBytes { get; init; }

    public required MirroringMode MirroringMode { get; init; }

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

    public required PpuBackgroundScanlineState[] BackgroundScanlineStates { get; init; }
}
