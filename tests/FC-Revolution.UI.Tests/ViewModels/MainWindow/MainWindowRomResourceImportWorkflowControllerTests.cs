using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowRomResourceImportWorkflowControllerTests
{
    [Fact]
    public void ImportPreviewVideo_DelegatesToImportAndPreviewReadyControllers_AndBuildsStatuses()
    {
        (string romPath, string sourcePath)? importArgs = null;
        RomLibraryItem? readyRom = null;
        string? readyPath = null;
        bool? readyIsCurrentRom = null;
        var tryLoadCalls = 0;
        var refreshCalls = 0;
        var syncCalls = 0;
        var controller = new MainWindowRomResourceImportWorkflowController(
            (romPath, sourcePath) =>
            {
                importArgs = (romPath, sourcePath);
                return new ImportedRomResource("preview.video", "contra-preview.mp4", "/tmp/contra-ready.mp4");
            },
            (_, _) => throw new InvalidOperationException("cover import should not run"),
            (_, _) => throw new InvalidOperationException("artwork import should not run"),
            (rom, previewPlaybackPath, isCurrentRom, tryLoadItemPreview, refreshCurrentRomState, syncCurrentPreviewBitmap) =>
            {
                readyRom = rom;
                readyPath = previewPlaybackPath;
                readyIsCurrentRom = isCurrentRom;
                tryLoadItemPreview(rom);
                refreshCurrentRomState();
                syncCurrentPreviewBitmap();
            });
        var rom = CreateRom("contra");

        var result = controller.ImportPreviewVideo(
            rom,
            "/tmp/in.mp4",
            isCurrentRom: true,
            tryLoadItemPreview: _ => tryLoadCalls++,
            refreshCurrentRomState: () => refreshCalls++,
            syncCurrentPreviewBitmap: () => syncCalls++);

        Assert.Equal((rom.Path, "/tmp/in.mp4"), importArgs);
        Assert.Same(rom, readyRom);
        Assert.Equal("/tmp/contra-ready.mp4", readyPath);
        Assert.True(readyIsCurrentRom);
        Assert.Equal(1, tryLoadCalls);
        Assert.Equal(1, refreshCalls);
        Assert.Equal(1, syncCalls);
        Assert.Equal("已导入预览视频: contra", result.StatusText);
        Assert.Equal("contra 已导入预览视频", result.PreviewStatusText);
    }

    [Fact]
    public void ImportCoverAndArtworkImage_DelegateAndBuildStatuses()
    {
        var coverCalls = new List<(string romPath, string sourcePath)>();
        var artworkCalls = new List<(string romPath, string sourcePath)>();
        var controller = new MainWindowRomResourceImportWorkflowController(
            (_, _) => throw new InvalidOperationException("preview import should not run"),
            (romPath, sourcePath) => coverCalls.Add((romPath, sourcePath)),
            (romPath, sourcePath) => artworkCalls.Add((romPath, sourcePath)),
            (_, _, _, _, _, _) => throw new InvalidOperationException("preview ready should not run"));
        var rom = CreateRom("mario");

        var coverResult = controller.ImportCoverImage(rom, "/tmp/cover.png");
        var artworkResult = controller.ImportArtworkImage(rom, "/tmp/art.png");

        Assert.Equal([(rom.Path, "/tmp/cover.png")], coverCalls);
        Assert.Equal([(rom.Path, "/tmp/art.png")], artworkCalls);
        Assert.Equal("已导入封面图: mario", coverResult.StatusText);
        Assert.Null(coverResult.PreviewStatusText);
        Assert.Equal("已导入附加图片: mario", artworkResult.StatusText);
        Assert.Null(artworkResult.PreviewStatusText);
    }

    private static RomLibraryItem CreateRom(string name) =>
        new($"{name}.nes", $"/tmp/{name}.nes", $"/tmp/{name}.mp4", hasPreview: false, fileSizeBytes: 1, importedAtUtc: DateTime.UtcNow);
}
