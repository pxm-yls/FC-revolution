using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Storage;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct ResourceCleanupSelection(
    bool CleanupPreviewAnimations,
    bool CleanupThumbnails,
    bool CleanupTimelineSaves,
    bool CleanupExportVideos)
{
    public bool HasAnySelection =>
        CleanupPreviewAnimations ||
        CleanupThumbnails ||
        CleanupTimelineSaves ||
        CleanupExportVideos;
}

internal readonly record struct ResourceCleanupSnapshot(
    int PreviewCount,
    int ImageCount,
    int TimelineFileCount,
    int ExportVideoCount);

internal readonly record struct ResourceCleanupResult(
    int RemovedPreviews,
    int RemovedImages,
    int RemovedTimelineFiles,
    int RemovedExportVideos)
{
    public string ToSummaryText() =>
        $"清理完成：预览动画 {RemovedPreviews} 个，缩略图/封面 {RemovedImages} 个，时间线存档文件 {RemovedTimelineFiles} 个，导出视频 {RemovedExportVideos} 个。";
}

internal sealed class MainWindowResourceManagementController
{
    private readonly IRomResourceImportService _romResourceImportService;
    private readonly MainWindowPreviewCleanupController _previewCleanupController;
    private readonly MainWindowRomAssociatedResourceController _romAssociatedResourceController;

    public MainWindowResourceManagementController(
        IRomResourceImportService romResourceImportService,
        MainWindowPreviewCleanupController? previewCleanupController = null,
        MainWindowRomAssociatedResourceController? romAssociatedResourceController = null)
    {
        _romResourceImportService = romResourceImportService;
        _previewCleanupController = previewCleanupController ?? new MainWindowPreviewCleanupController();
        _romAssociatedResourceController = romAssociatedResourceController ?? new MainWindowRomAssociatedResourceController();
    }

    public string ConfigureResourceRoot(string? input)
    {
        var resolved = string.IsNullOrWhiteSpace(input)
            ? AppObjectStorage.GetDefaultResourceRoot()
            : Path.GetFullPath(input.Trim());

        AppObjectStorage.ConfigureResourceRoot(resolved);
        AppObjectStorage.EnsureDefaults();
        return AppObjectStorage.GetResourceRoot();
    }

    public ImportedRomResource ImportPreviewVideo(string romPath, string sourcePath)
    {
        return _romResourceImportService.ImportPreviewVideo(romPath, sourcePath);
    }

    public void ImportCoverImage(string romPath, string sourcePath)
    {
        _romResourceImportService.ImportCoverImage(romPath, sourcePath);
    }

    public void ImportArtworkImage(string romPath, string sourcePath)
    {
        _romResourceImportService.ImportArtworkImage(romPath, sourcePath);
    }

    public ResourceCleanupResult ExecuteCleanup(ResourceCleanupSelection selection, IEnumerable<RomLibraryItem> romLibrary)
    {
        var removedPreviews = 0;
        var removedImages = 0;
        var removedTimelineFiles = 0;
        var removedExportVideos = 0;

        if (selection.CleanupPreviewAnimations)
        {
            removedPreviews += DeleteFilesUnderDirectory(AppObjectStorage.GetPreviewVideosDirectory());
            removedPreviews += DeleteFilesUnderDirectory(AppObjectStorage.GetLegacyPreviewVideosDirectory());
        }

        if (selection.CleanupThumbnails)
            removedImages += DeleteFilesUnderDirectory(AppObjectStorage.GetImagesDirectory());

        if (selection.CleanupTimelineSaves)
        {
            removedTimelineFiles += DeleteFilesUnderDirectory(
                AppObjectStorage.GetTimelineRootDirectory(),
                path => !string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase));
        }

        if (selection.CleanupExportVideos)
            removedExportVideos += DeleteExportVideosUnderTimelineRoot(AppObjectStorage.GetTimelineRootDirectory());

        _previewCleanupController.ClearPreviewFrames(romLibrary, selection.CleanupPreviewAnimations);

        return new ResourceCleanupResult(
            removedPreviews,
            removedImages,
            removedTimelineFiles,
            removedExportVideos);
    }

    public ResourceCleanupSnapshot CaptureCleanupSnapshot()
    {
        var previewCount = CountFiles(AppObjectStorage.GetPreviewVideosDirectory()) + CountFiles(AppObjectStorage.GetLegacyPreviewVideosDirectory());
        var imageCount = CountFiles(AppObjectStorage.GetImagesDirectory());
        var timelineFileCount = CountFiles(AppObjectStorage.GetTimelineRootDirectory());
        var exportCount = CountFiles(AppObjectStorage.GetTimelineRootDirectory(), "*.mp4");
        return new ResourceCleanupSnapshot(previewCount, imageCount, timelineFileCount, exportCount);
    }

    public void DeleteRomAssociatedResources(string romPath)
        => _romAssociatedResourceController.DeleteRomAssociatedResources(romPath);

    public string BuildRomAssociatedResourceSummary(string romPath)
        => _romAssociatedResourceController.BuildRomAssociatedResourceSummary(romPath);

    private static int DeleteFilesUnderDirectory(string directoryPath, Func<string, bool>? filter = null)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            if (filter != null && !filter(file))
                continue;

            File.Delete(file);
            deleted++;
        }

        CleanupEmptyDirectories(directoryPath, preserveRoot: true);
        return deleted;
    }

    private static int DeleteExportVideosUnderTimelineRoot(string timelineRoot)
    {
        if (!Directory.Exists(timelineRoot))
            return 0;

        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(timelineRoot, "*.mp4", SearchOption.AllDirectories))
        {
            File.Delete(file);
            deleted++;
        }

        CleanupEmptyDirectories(timelineRoot, preserveRoot: true);
        return deleted;
    }

    private static void CleanupEmptyDirectories(string rootDirectory, bool preserveRoot)
    {
        if (!Directory.Exists(rootDirectory))
            return;

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory, recursive: false);
        }

        if (!preserveRoot && !Directory.EnumerateFileSystemEntries(rootDirectory).Any())
            Directory.Delete(rootDirectory, recursive: false);
    }

    private static int CountFiles(string directoryPath, string searchPattern = "*")
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        try
        {
            return Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }

}
