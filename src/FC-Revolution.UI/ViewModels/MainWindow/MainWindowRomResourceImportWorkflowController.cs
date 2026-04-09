using System;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct RomResourceImportWorkflowResult(
    string StatusText,
    string? PreviewStatusText = null);

internal sealed class MainWindowRomResourceImportWorkflowController
{
    private readonly Func<string, string, ImportedRomResource> _importPreviewVideo;
    private readonly Action<string, string> _importCoverImage;
    private readonly Action<string, string> _importArtworkImage;
    private readonly Action<RomLibraryItem, string, bool, Action<RomLibraryItem>, Action, Action> _applyPreviewAssetReady;

    public MainWindowRomResourceImportWorkflowController(
        MainWindowResourceManagementController resourceManagementController,
        MainWindowPreviewAssetReadyController previewAssetReadyController)
        : this(
            resourceManagementController.ImportPreviewVideo,
            resourceManagementController.ImportCoverImage,
            resourceManagementController.ImportArtworkImage,
            (rom, previewPlaybackPath, isCurrentRom, tryLoadItemPreview, refreshCurrentRomState, syncCurrentPreviewBitmap) =>
                previewAssetReadyController.ApplyPreviewAssetReady(
                    rom,
                    previewPlaybackPath,
                    isCurrentRom,
                    syncCurrentPreviewFrame: false,
                    tryLoadItemPreview,
                    refreshCurrentRomState,
                    syncCurrentPreviewBitmap))
    {
    }

    internal MainWindowRomResourceImportWorkflowController(
        Func<string, string, ImportedRomResource> importPreviewVideo,
        Action<string, string> importCoverImage,
        Action<string, string> importArtworkImage,
        Action<RomLibraryItem, string, bool, Action<RomLibraryItem>, Action, Action> applyPreviewAssetReady)
    {
        _importPreviewVideo = importPreviewVideo;
        _importCoverImage = importCoverImage;
        _importArtworkImage = importArtworkImage;
        _applyPreviewAssetReady = applyPreviewAssetReady;
    }

    public RomResourceImportWorkflowResult ImportPreviewVideo(
        RomLibraryItem rom,
        string sourcePath,
        bool isCurrentRom,
        Action<RomLibraryItem> tryLoadItemPreview,
        Action refreshCurrentRomState,
        Action syncCurrentPreviewBitmap)
    {
        var imported = _importPreviewVideo(rom.Path, sourcePath);
        _applyPreviewAssetReady(
            rom,
            imported.AbsolutePath,
            isCurrentRom,
            tryLoadItemPreview,
            refreshCurrentRomState,
            syncCurrentPreviewBitmap);

        return new(
            StatusText: $"已导入预览视频: {rom.DisplayName}",
            PreviewStatusText: $"{rom.DisplayName} 已导入预览视频");
    }

    public RomResourceImportWorkflowResult ImportCoverImage(RomLibraryItem rom, string sourcePath)
    {
        _importCoverImage(rom.Path, sourcePath);
        return new(StatusText: $"已导入封面图: {rom.DisplayName}");
    }

    public RomResourceImportWorkflowResult ImportArtworkImage(RomLibraryItem rom, string sourcePath)
    {
        _importArtworkImage(rom.Path, sourcePath);
        return new(StatusText: $"已导入附加图片: {rom.DisplayName}");
    }
}
