using FCRevolution.Rendering.Abstractions;
using FCRevolution.Backend.Hosting;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowFramePresenterControllerTests
{
    [Fact]
    public void EnqueueCoreFrame_PublishesPendingPresentation_AndLastPresentedState()
    {
        using var controller = new GameWindowFramePresenterController(256, 240);
        var frame = CreateSolidFrame(0xFF00FF00u);
        var layeredFrame = CreateLayeredFrame();

        controller.EnqueueCoreFrame(frame, layeredFrame);

        Assert.Equal(frame, controller.LastPresentedFrame);
        Assert.Same(layeredFrame, controller.LastPresentedLayeredFrame);
        Assert.True(controller.TryTakePendingPresentation(out var presentation));
        Assert.Same(frame, presentation.FrameBuffer);
        Assert.Same(layeredFrame, presentation.LayeredFrame);
    }

    [Fact]
    public void PresentSlot_GatesConcurrentQueueing_UntilReleased()
    {
        using var controller = new GameWindowFramePresenterController(256, 240);
        controller.SetPendingPreviewFrame(CreateSolidFrame(0xFF0000FFu));

        Assert.True(controller.TryAcquirePresentSlot());
        Assert.False(controller.TryAcquirePresentSlot());
        Assert.True(controller.ReleasePresentSlotAndCheckForPending());
        Assert.True(controller.TryTakePendingPresentation(out _));

        controller.SetPendingPreviewFrame(CreateSolidFrame(0xFFFF0000u));
        Assert.True(controller.ReleasePresentSlotAndCheckForPending());
        Assert.True(controller.TryAcquirePresentSlot());
    }

    [Fact]
    public void PresentFrame_SwapsBitmapInstances()
    {
        using var controller = new GameWindowFramePresenterController(256, 240);
        var initialBitmap = controller.CurrentBitmap;

        var firstPresented = controller.PresentFrame(CreateSolidFrame(0xFF0000FFu), PixelEnhancementMode.None);
        var secondPresented = controller.PresentFrame(CreateSolidFrame(0xFF00FF00u), PixelEnhancementMode.CrtScanlines);

        Assert.NotSame(initialBitmap, firstPresented);
        Assert.NotSame(firstPresented, secondPresented);
        Assert.Same(secondPresented, controller.CurrentBitmap);
    }

    private static uint[] CreateSolidFrame(uint pixel)
    {
        var frame = new uint[256 * 240];
        Array.Fill(frame, pixel);
        return frame;
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
