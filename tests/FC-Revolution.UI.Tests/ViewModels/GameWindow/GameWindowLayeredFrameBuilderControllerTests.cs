using FCRevolution.Rendering.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowLayeredFrameBuilderControllerTests
{
    [Fact]
    public void TryBuildLayeredFrame_ReturnsProviderOutput_AndForwardsTemporalReset()
    {
        var layeredFrames = new Queue<LayeredFrameData>(
        [
            CreateLayeredFrame(),
            CreateLayeredFrame(),
            CreateLayeredFrame()
        ]);
        var resetCount = 0;

        var controller = new GameWindowLayeredFrameBuilderController(
            () => layeredFrames.Dequeue(),
            () => resetCount++,
            _ => { });

        Assert.NotNull(controller.TryBuildLayeredFrame());
        Assert.NotNull(controller.TryBuildLayeredFrame());
        controller.ResetTemporalHistory();
        Assert.NotNull(controller.TryBuildLayeredFrame());
        Assert.Equal(1, resetCount);
    }

    [Fact]
    public void TryBuildLayeredFrame_OnFailure_LogsOnce_AndResetsProviderHistory()
    {
        var shouldThrow = false;
        var logCount = 0;
        var resetCount = 0;

        var controller = new GameWindowLayeredFrameBuilderController(
            () =>
            {
                if (shouldThrow)
                    throw new InvalidOperationException("boom");

                return CreateLayeredFrame();
            },
            () => resetCount++,
            _ => logCount++);

        Assert.NotNull(controller.TryBuildLayeredFrame());
        shouldThrow = true;
        Assert.Null(controller.TryBuildLayeredFrame());
        Assert.Null(controller.TryBuildLayeredFrame());
        shouldThrow = false;
        Assert.NotNull(controller.TryBuildLayeredFrame());

        Assert.Equal(1, logCount);
        Assert.Equal(2, resetCount);
    }

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
