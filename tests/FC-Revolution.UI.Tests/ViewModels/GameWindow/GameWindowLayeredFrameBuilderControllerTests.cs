using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Rendering.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowLayeredFrameBuilderControllerTests
{
    [Fact]
    public void TryBuildLayeredFrame_PassesPreviousMetadata_UntilTemporalReset()
    {
        var snapshot = CreateSnapshot();
        var previousMetadataSeen = new List<IFrameMetadata?>();
        var metadataSequence = new Queue<FrameMetadata>(
        [
            new FrameMetadata(),
            new FrameMetadata(),
            new FrameMetadata()
        ]);
        var layeredFrames = new Queue<LayeredFrameData>(
        [
            CreateLayeredFrame(),
            CreateLayeredFrame(),
            CreateLayeredFrame()
        ]);

        var controller = new GameWindowLayeredFrameBuilderController(
            () => snapshot,
            (_, previousMetadata) =>
            {
                previousMetadataSeen.Add(previousMetadata);
                return metadataSequence.Dequeue();
            },
            _ => layeredFrames.Dequeue(),
            _ => { });

        Assert.NotNull(controller.TryBuildLayeredFrame());
        Assert.NotNull(controller.TryBuildLayeredFrame());
        controller.ResetTemporalHistory();
        Assert.NotNull(controller.TryBuildLayeredFrame());

        Assert.Null(previousMetadataSeen[0]);
        Assert.NotNull(previousMetadataSeen[1]);
        Assert.Null(previousMetadataSeen[2]);
    }

    [Fact]
    public void TryBuildLayeredFrame_OnFailure_LogsOnce_AndClearsMetadataHistory()
    {
        var snapshot = CreateSnapshot();
        var previousMetadataSeen = new List<IFrameMetadata?>();
        var shouldThrow = false;
        var logCount = 0;
        var metadataSequence = new Queue<FrameMetadata>(
        [
            new FrameMetadata(),
            new FrameMetadata()
        ]);

        var controller = new GameWindowLayeredFrameBuilderController(
            () =>
            {
                if (shouldThrow)
                    throw new InvalidOperationException("boom");

                return snapshot;
            },
            (_, previousMetadata) =>
            {
                previousMetadataSeen.Add(previousMetadata);
                return metadataSequence.Dequeue();
            },
            _ => CreateLayeredFrame(),
            _ => logCount++);

        Assert.NotNull(controller.TryBuildLayeredFrame());
        shouldThrow = true;
        Assert.Null(controller.TryBuildLayeredFrame());
        Assert.Null(controller.TryBuildLayeredFrame());
        shouldThrow = false;
        Assert.NotNull(controller.TryBuildLayeredFrame());

        Assert.Equal(1, logCount);
        Assert.Null(previousMetadataSeen[0]);
        Assert.Null(previousMetadataSeen[1]);
    }

    private static PpuRenderStateSnapshot CreateSnapshot() =>
        new()
        {
            NametableBytes = new byte[2048],
            PatternTableBytes = new byte[8192],
            PaletteColors = new uint[32],
            OamBytes = new byte[256],
            MirroringMode = MirroringMode.Horizontal,
            FineScrollX = 0,
            FineScrollY = 0,
            CoarseScrollX = 0,
            CoarseScrollY = 0,
            NametableSelect = 0,
            UseBackgroundPatternTableHighBank = false,
            UseSpritePatternTableHighBank = false,
            Use8x16Sprites = false,
            ShowBackground = true,
            ShowSprites = true,
            ShowBackgroundLeft8 = true,
            ShowSpritesLeft8 = true,
            HasCapturedBackgroundScanlineStates = false,
            BackgroundScanlineStates = []
        };

    private static LayeredFrameData CreateLayeredFrame() =>
        new(
            256,
            240,
            [],
            [],
            [],
            [],
            showBackground: true,
            showSprites: true,
            showBackgroundLeft8: true,
            showSpritesLeft8: true);
}
