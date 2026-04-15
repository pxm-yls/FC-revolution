using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Rendering.Abstractions;

namespace FC_Revolution.Rendering.Tests;

internal static class RenderSnapshotTestAdapter
{
    public static RenderStateSnapshot FromPpu(PpuRenderStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new RenderStateSnapshot
        {
            BackgroundPlaneBytes = snapshot.NametableBytes,
            TileGraphicsBytes = snapshot.PatternTableBytes,
            PaletteColors = snapshot.PaletteColors,
            SpriteBytes = snapshot.OamBytes,
            BackgroundPlaneLayout = snapshot.MirroringMode switch
            {
                MirroringMode.Horizontal => BackgroundPlaneLayoutMode.SharedTopBottom,
                MirroringMode.Vertical => BackgroundPlaneLayoutMode.SharedLeftRight,
                MirroringMode.SingleLower => BackgroundPlaneLayoutMode.SinglePlane0,
                MirroringMode.SingleUpper => BackgroundPlaneLayoutMode.SinglePlane1,
                MirroringMode.FourScreen => BackgroundPlaneLayoutMode.IndependentPlanes,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.MirroringMode, "Unsupported mirroring mode.")
            },
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
                .Select(scanline => new BackgroundScanlineRenderState
                {
                    FineScrollX = scanline.FineScrollX,
                    FineScrollY = scanline.FineScrollY,
                    CoarseScrollX = scanline.CoarseScrollX,
                    CoarseScrollY = scanline.CoarseScrollY,
                    BackgroundPlaneSelect = scanline.NametableSelect,
                    UseUpperBackgroundTileBank = scanline.UseBackgroundPatternTableHighBank,
                    ShowBackground = scanline.ShowBackground,
                    ShowBackgroundInFirstTileColumn = scanline.ShowBackgroundLeft8
                })
                .ToArray()
        };
    }
}
