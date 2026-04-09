using System;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewSelectionControllerTests
{
    [Fact]
    public void ApplyCurrentRomSelection_LoadedCurrentRomWithAnimatedPreview_StartsPlayback()
    {
        var controller = new MainWindowPreviewSelectionController();
        var currentRom = CreateRom("contra", "/tmp/contra.nes");
        var startCalls = 0;
        var stopCalls = 0;
        var discUpdateCalls = 0;
        var warmupCalls = 0;
        RomLibraryItem? syncedRom = null;
        RomLibraryItem? warmedRom = null;

        controller.ApplyCurrentRomSelection(
            currentRom,
            hasLoadedCurrentRomPreview: true,
            hasAnyAnimatedPreview: true,
            syncCurrentPreviewBitmap: rom => syncedRom = rom,
            startPreviewPlayback: () => startCalls++,
            stopPreviewPlayback: () => stopCalls++,
            updateDiscDisplayBitmap: () => discUpdateCalls++,
            requestPreviewWarmup: rom =>
            {
                warmupCalls++;
                warmedRom = rom;
            });

        Assert.Same(currentRom, syncedRom);
        Assert.Same(currentRom, warmedRom);
        Assert.Equal(1, startCalls);
        Assert.Equal(0, stopCalls);
        Assert.Equal(1, discUpdateCalls);
        Assert.Equal(1, warmupCalls);
    }

    [Fact]
    public void ApplyCurrentRomSelection_NoAnimatedPreview_StopsPlayback()
    {
        var controller = new MainWindowPreviewSelectionController();
        var currentRom = CreateRom("mario", "/tmp/mario.nes");
        var startCalls = 0;
        var stopCalls = 0;

        controller.ApplyCurrentRomSelection(
            currentRom,
            hasLoadedCurrentRomPreview: false,
            hasAnyAnimatedPreview: false,
            syncCurrentPreviewBitmap: _ => { },
            startPreviewPlayback: () => startCalls++,
            stopPreviewPlayback: () => stopCalls++,
            updateDiscDisplayBitmap: () => { },
            requestPreviewWarmup: _ => { });

        Assert.Equal(0, startCalls);
        Assert.Equal(1, stopCalls);
    }

    [Fact]
    public void HandleEmptyLibrary_StopsPlaybackAndClearsCurrentPreview()
    {
        var controller = new MainWindowPreviewSelectionController();
        var stopCalls = 0;
        var clearCalls = 0;

        controller.HandleEmptyLibrary(
            stopPreviewPlayback: () => stopCalls++,
            clearCurrentPreviewBitmap: () => clearCalls++);

        Assert.Equal(1, stopCalls);
        Assert.Equal(1, clearCalls);
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
