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
            BackgroundPlaneBytes = snapshot.NametableBytes,
            TileGraphicsBytes = snapshot.PatternTableBytes,
            PaletteColors = snapshot.PaletteColors,
            SpriteBytes = snapshot.OamBytes,
            BackgroundPlaneLayout = MapBackgroundPlaneLayout(snapshot.MirroringMode),
            FineScrollX = snapshot.FineScrollX,
            FineScrollY = snapshot.FineScrollY,
            CoarseScrollX = snapshot.CoarseScrollX,
            CoarseScrollY = snapshot.CoarseScrollY,
            BackgroundPlaneSelect = snapshot.NametableSelect,
            UseUpperBackgroundTileBank = snapshot.UseBackgroundPatternTableHighBank,
            UseUpperSpriteTileBank = snapshot.UseSpritePatternTableHighBank,
            UseTallSprites = snapshot.Use8x16Sprites,
            ShowBackground = snapshot.ShowBackground,
            ShowSprites = snapshot.ShowSprites,
            ShowBackgroundInFirstTileColumn = snapshot.ShowBackgroundLeft8,
            ShowSpritesInFirstTileColumn = snapshot.ShowSpritesLeft8,
            HasCapturedBackgroundScanlineStates = snapshot.HasCapturedBackgroundScanlineStates,
            BackgroundScanlineStates = snapshot.BackgroundScanlineStates
                .Select(MapScanlineState)
                .ToArray()
        };
    }

    private static BackgroundPlaneLayoutMode MapBackgroundPlaneLayout(MirroringMode mirroringMode) =>
        mirroringMode switch
        {
            MirroringMode.Horizontal => BackgroundPlaneLayoutMode.SharedTopBottom,
            MirroringMode.Vertical => BackgroundPlaneLayoutMode.SharedLeftRight,
            MirroringMode.SingleLower => BackgroundPlaneLayoutMode.SinglePlane0,
            MirroringMode.SingleUpper => BackgroundPlaneLayoutMode.SinglePlane1,
            MirroringMode.FourScreen => BackgroundPlaneLayoutMode.IndependentPlanes,
            _ => throw new ArgumentOutOfRangeException(nameof(mirroringMode), mirroringMode, "Unsupported mirroring mode.")
        };

    private static BackgroundScanlineRenderState MapScanlineState(PpuBackgroundScanlineState scanlineState) =>
        new()
        {
            FineScrollX = scanlineState.FineScrollX,
            FineScrollY = scanlineState.FineScrollY,
            CoarseScrollX = scanlineState.CoarseScrollX,
            CoarseScrollY = scanlineState.CoarseScrollY,
            BackgroundPlaneSelect = scanlineState.NametableSelect,
            UseUpperBackgroundTileBank = scanlineState.UseBackgroundPatternTableHighBank,
            ShowBackground = scanlineState.ShowBackground,
            ShowBackgroundInFirstTileColumn = scanlineState.ShowBackgroundLeft8
        };
}
