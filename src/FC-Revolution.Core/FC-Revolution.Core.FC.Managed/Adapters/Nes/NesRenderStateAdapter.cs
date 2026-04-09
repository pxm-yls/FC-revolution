using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Core.Nes.Managed.Adapters.Nes;

internal static class NesRenderStateAdapter
{
    public static RenderStateSnapshot Map(PpuRenderStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new RenderStateSnapshot
        {
            NametableBytes = snapshot.NametableBytes,
            PatternTableBytes = snapshot.PatternTableBytes,
            PaletteColors = snapshot.PaletteColors,
            OamBytes = snapshot.OamBytes,
            MirroringMode = MapMirroringMode(snapshot.MirroringMode),
            FineScrollX = snapshot.FineScrollX,
            FineScrollY = snapshot.FineScrollY,
            CoarseScrollX = snapshot.CoarseScrollX,
            CoarseScrollY = snapshot.CoarseScrollY,
            NametableSelect = snapshot.NametableSelect,
            UseBackgroundPatternTableHighBank = snapshot.UseBackgroundPatternTableHighBank,
            UseSpritePatternTableHighBank = snapshot.UseSpritePatternTableHighBank,
            Use8x16Sprites = snapshot.Use8x16Sprites,
            ShowBackground = snapshot.ShowBackground,
            ShowSprites = snapshot.ShowSprites,
            ShowBackgroundLeft8 = snapshot.ShowBackgroundLeft8,
            ShowSpritesLeft8 = snapshot.ShowSpritesLeft8,
            HasCapturedBackgroundScanlineStates = snapshot.HasCapturedBackgroundScanlineStates,
            BackgroundScanlineStates = snapshot.BackgroundScanlineStates
                .Select(MapScanlineState)
                .ToArray()
        };
    }

    private static FrameMirroringMode MapMirroringMode(MirroringMode mirroringMode) =>
        mirroringMode switch
        {
            MirroringMode.Horizontal => FrameMirroringMode.Horizontal,
            MirroringMode.Vertical => FrameMirroringMode.Vertical,
            MirroringMode.SingleLower => FrameMirroringMode.SingleLower,
            MirroringMode.SingleUpper => FrameMirroringMode.SingleUpper,
            MirroringMode.FourScreen => FrameMirroringMode.FourScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(mirroringMode), mirroringMode, "Unsupported mirroring mode.")
        };

    private static BackgroundScanlineRenderState MapScanlineState(PpuBackgroundScanlineState scanlineState) =>
        new()
        {
            FineScrollX = scanlineState.FineScrollX,
            FineScrollY = scanlineState.FineScrollY,
            CoarseScrollX = scanlineState.CoarseScrollX,
            CoarseScrollY = scanlineState.CoarseScrollY,
            NametableSelect = scanlineState.NametableSelect,
            UseBackgroundPatternTableHighBank = scanlineState.UseBackgroundPatternTableHighBank,
            ShowBackground = scanlineState.ShowBackground,
            ShowBackgroundLeft8 = scanlineState.ShowBackgroundLeft8
        };
}
