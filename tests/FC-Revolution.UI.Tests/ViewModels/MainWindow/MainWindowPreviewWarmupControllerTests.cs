using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewWarmupControllerTests
{
    [Fact]
    public async Task WarmPreviewFramesAsync_UsesKeepAliveAndAppliesTrimEviction()
    {
        var playbackPolicy = new MainWindowPreviewPlaybackPolicyController();
        var controller = new MainWindowPreviewWarmupController(playbackPolicy);
        var roms = new[]
        {
            CreateRom("R1", "/tmp/r1.nes"),
            CreateRom("R2", "/tmp/r2.nes"),
            CreateRom("R3", "/tmp/r3.nes"),
            CreateRom("R4", "/tmp/r4.nes"),
            CreateRom("R5", "/tmp/r5.nes")
        };
        var loadedPaths = roms.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clearedPaths = new List<string>();
        HashSet<string>? capturedKeepAlivePaths = null;

        await controller.WarmPreviewFramesAsync(
            CancellationToken.None,
            roms,
            priorityRom: roms[2],
            isShelfLayoutMode: true,
            isKaleidoscopeMode: false,
            shelfVisibleRowCount: 1,
            shelfWarmExtraRows: 1,
            shelfColumns: 4,
            kaleidoscopePageSize: 8,
            maxWarmedPreviews: 2,
            visibleShelfWarmTargets: [roms[0]],
            visibleKaleidoscopeTargets: [],
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            hasPreviewSelector: _ => true,
            hasLoadedPreviewSelector: rom => loadedPaths.Contains(rom.Path),
            isPreviewCandidateSelector: _ => true,
            trimCacheAsync: (items, keepAlivePaths, priorityRom) =>
            {
                capturedKeepAlivePaths = keepAlivePaths;
                controller.TrimLoadedPreviewCache(
                    items,
                    keepAlivePaths,
                    priorityRom,
                    maxLoadedPreviewCache: 3,
                    isLoadedSelector: rom => loadedPaths.Contains(rom.Path),
                    pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                    clearPreviewFrames: rom =>
                    {
                        clearedPaths.Add(rom.Path);
                        loadedPaths.Remove(rom.Path);
                    });
                return Task.CompletedTask;
            },
            warmItemAsync: (_, _) => Task.CompletedTask,
            updatePlaybackStateAsync: () => Task.CompletedTask,
            primeAnimatedPreviewsAsync: () => Task.CompletedTask);

        Assert.NotNull(capturedKeepAlivePaths);
        Assert.Contains("/tmp/r1.nes", capturedKeepAlivePaths!);
        Assert.Equal(2, clearedPaths.Count);
        Assert.DoesNotContain("/tmp/r1.nes", clearedPaths);
    }

    [Fact]
    public async Task PrimeAnimatedPreviewsAsync_AppliesEnableDisableAndMemoryCleanupByTargetPaths()
    {
        var playbackPolicy = new MainWindowPreviewPlaybackPolicyController();
        var controller = new MainWindowPreviewWarmupController(playbackPolicy);
        var rom1 = CreateRom("R1", "/tmp/r1.nes");
        var rom2 = CreateRom("R2", "/tmp/r2.nes");
        var rom3 = CreateRom("R3", "/tmp/r3.nes");
        var allRoms = new[] { rom1, rom2, rom3 };
        var loadedPaths = new HashSet<string>(allRoms.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
        var animatedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rom1.Path, rom2.Path };
        var memoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rom3.Path };
        var enabled = new List<string>();
        var disabled = new List<string>();
        var memoryDisabled = new List<string>();

        await controller.PrimeAnimatedPreviewsAsync(
            isCarouselMode: false,
            isShelfLayoutMode: true,
            isKaleidoscopeMode: false,
            isShelfScrolling: false,
            currentRom: null,
            previousRom: null,
            nextRom: null,
            shelfTargets: [rom1, rom2],
            kaleidoscopeTargets: [],
            allRoms,
            maxShelfSmoothPlayback: 1,
            kaleidoscopePageSize: 8,
            maxMemoryAnimatedPreviews: 4,
            isLoadedSelector: rom => loadedPaths.Contains(rom.Path),
            isAnimatedSelector: rom => animatedPaths.Contains(rom.Path),
            isMemoryPreviewSelector: rom => memoryPaths.Contains(rom.Path),
            enableSmoothPlayback: rom => enabled.Add(rom.Path),
            disableSmoothPlayback: rom => disabled.Add(rom.Path),
            disableMemoryPlayback: rom => memoryDisabled.Add(rom.Path));

        Assert.Equal([rom1.Path], enabled);
        Assert.Contains(rom2.Path, disabled);
        Assert.Contains(rom3.Path, disabled);
        Assert.Equal([rom3.Path], memoryDisabled);
    }

    [Fact]
    public async Task PrimeAnimatedPreviewsAsync_ShortCircuitsWhenShelfIsScrolling()
    {
        var playbackPolicy = new MainWindowPreviewPlaybackPolicyController();
        var controller = new MainWindowPreviewWarmupController(playbackPolicy);
        var rom1 = CreateRom("R1", "/tmp/r1.nes");
        var rom2 = CreateRom("R2", "/tmp/r2.nes");
        var enabled = new List<string>();
        var disabled = new List<string>();
        var memoryDisabled = new List<string>();

        await controller.PrimeAnimatedPreviewsAsync(
            isCarouselMode: false,
            isShelfLayoutMode: true,
            isKaleidoscopeMode: false,
            isShelfScrolling: true,
            currentRom: null,
            previousRom: null,
            nextRom: null,
            shelfTargets: [rom1, rom2],
            kaleidoscopeTargets: [],
            allRoms: [rom1, rom2],
            maxShelfSmoothPlayback: 1,
            kaleidoscopePageSize: 8,
            maxMemoryAnimatedPreviews: 4,
            isLoadedSelector: _ => true,
            isAnimatedSelector: _ => true,
            isMemoryPreviewSelector: _ => true,
            enableSmoothPlayback: rom => enabled.Add(rom.Path),
            disableSmoothPlayback: rom => disabled.Add(rom.Path),
            disableMemoryPlayback: rom => memoryDisabled.Add(rom.Path));

        Assert.Empty(enabled);
        Assert.Empty(disabled);
        Assert.Empty(memoryDisabled);
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}
