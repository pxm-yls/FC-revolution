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
            NametableBytes = snapshot.NametableBytes,
            PatternTableBytes = snapshot.PatternTableBytes,
            PaletteColors = snapshot.PaletteColors,
            OamBytes = snapshot.OamBytes,
            MirroringMode = snapshot.MirroringMode switch
            {
                MirroringMode.Horizontal => FrameMirroringMode.Horizontal,
                MirroringMode.Vertical => FrameMirroringMode.Vertical,
                MirroringMode.SingleLower => FrameMirroringMode.SingleLower,
                MirroringMode.SingleUpper => FrameMirroringMode.SingleUpper,
                MirroringMode.FourScreen => FrameMirroringMode.FourScreen,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.MirroringMode, "Unsupported mirroring mode.")
            },
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
                .Select(scanline => new BackgroundScanlineRenderState
                {
                    FineScrollX = scanline.FineScrollX,
                    FineScrollY = scanline.FineScrollY,
                    CoarseScrollX = scanline.CoarseScrollX,
                    CoarseScrollY = scanline.CoarseScrollY,
                    NametableSelect = scanline.NametableSelect,
                    UseBackgroundPatternTableHighBank = scanline.UseBackgroundPatternTableHighBank,
                    ShowBackground = scanline.ShowBackground,
                    ShowBackgroundLeft8 = scanline.ShowBackgroundLeft8
                })
                .ToArray()
        };
    }
}
