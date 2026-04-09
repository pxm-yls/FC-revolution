using System;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewWarmupRequestControllerTests
{
    [Fact]
    public void Enqueue_WhenIdle_StartsProcessorWithoutCancel()
    {
        var controller = new MainWindowPreviewWarmupRequestController();
        var rom = CreateRom("contra", "/tmp/contra.nes");

        var decision = controller.Enqueue(rom);

        Assert.True(decision.ShouldStartProcessor);
        Assert.False(decision.ShouldCancelActiveWarmup);
        Assert.True(controller.TryDequeue(out var dequeuedRom));
        Assert.Same(rom, dequeuedRom);
    }

    [Fact]
    public void Enqueue_WhenProcessorRunning_CoalescesToLatestAndRequestsCancel()
    {
        var controller = new MainWindowPreviewWarmupRequestController();
        var firstRom = CreateRom("mario", "/tmp/mario.nes");
        var latestRom = CreateRom("zelda", "/tmp/zelda.nes");

        var firstDecision = controller.Enqueue(firstRom);
        Assert.True(firstDecision.ShouldStartProcessor);
        Assert.True(controller.TryDequeue(out var inFlightRom));
        Assert.Same(firstRom, inFlightRom);

        var secondDecision = controller.Enqueue(latestRom);

        Assert.False(secondDecision.ShouldStartProcessor);
        Assert.True(secondDecision.ShouldCancelActiveWarmup);
        Assert.True(controller.TryDequeue(out var dequeuedRom));
        Assert.Same(latestRom, dequeuedRom);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ResetsProcessorStateForNextRequest()
    {
        var controller = new MainWindowPreviewWarmupRequestController();

        _ = controller.Enqueue(null);
        Assert.True(controller.TryDequeue(out _));
        Assert.False(controller.TryDequeue(out _));

        var nextDecision = controller.Enqueue(null);

        Assert.True(nextDecision.ShouldStartProcessor);
        Assert.False(nextDecision.ShouldCancelActiveWarmup);
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
