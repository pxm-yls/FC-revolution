using FCRevolution.Storage;
using FCRevolution.Core.Timeline.Persistence;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowResourceManagementControllerTests
{
    [Fact]
    public void ExecuteCleanup_RemovesSelectedResourceFilesAndUpdatesRomPreviewFlags()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-resource-cleanup-tests-{Guid.NewGuid():N}");

        try
        {
            var importService = new RecordingRomResourceImportService();
            var controller = new MainWindowResourceManagementController(importService);
            controller.ConfigureResourceRoot(tempRoot);

            var previewFile = WriteFile(Path.Combine(AppObjectStorage.GetPreviewVideosDirectory(), "a.mp4"));
            var legacyPreviewFile = WriteFile(Path.Combine(AppObjectStorage.GetLegacyPreviewVideosDirectory(), "legacy.mov"));
            var imageFile = WriteFile(Path.Combine(AppObjectStorage.GetImagesDirectory(), "cover.png"));
            var timelineSaveFile = WriteFile(Path.Combine(AppObjectStorage.GetTimelineRootDirectory(), "timeline.bin"));
            var exportVideoFile = WriteFile(Path.Combine(AppObjectStorage.GetTimelineRootDirectory(), "export.mp4"));
            Assert.All([previewFile, legacyPreviewFile, imageFile, timelineSaveFile, exportVideoFile], path => Assert.True(File.Exists(path)));

            var rom = new RomLibraryItem(
                "demo.nes",
                Path.Combine(tempRoot, "roms", "demo.nes"),
                previewFile,
                hasPreview: true,
                fileSizeBytes: 1024,
                importedAtUtc: DateTime.UtcNow);

            var before = controller.CaptureCleanupSnapshot();
            Assert.Equal(2, before.PreviewCount);
            Assert.Equal(1, before.ImageCount);
            Assert.Equal(2, before.TimelineFileCount);
            Assert.Equal(1, before.ExportVideoCount);

            var result = controller.ExecuteCleanup(
                new ResourceCleanupSelection(
                    CleanupPreviewAnimations: true,
                    CleanupThumbnails: true,
                    CleanupTimelineSaves: true,
                    CleanupExportVideos: true),
                [rom]);

            Assert.Equal(2, result.RemovedPreviews);
            Assert.Equal(1, result.RemovedImages);
            Assert.Equal(1, result.RemovedTimelineFiles);
            Assert.Equal(1, result.RemovedExportVideos);
            Assert.False(rom.HasPreview);

            var after = controller.CaptureCleanupSnapshot();
            Assert.Equal(0, after.PreviewCount);
            Assert.Equal(0, after.ImageCount);
            Assert.Equal(0, after.TimelineFileCount);
            Assert.Equal(0, after.ExportVideoCount);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ImportMethods_DelegateToImportService()
    {
        var importService = new RecordingRomResourceImportService();
        var controller = new MainWindowResourceManagementController(importService);
        var romPath = "/tmp/demo.nes";

        var imported = controller.ImportPreviewVideo(romPath, "/tmp/in.mp4");
        controller.ImportCoverImage(romPath, "/tmp/cover.png");
        controller.ImportArtworkImage(romPath, "/tmp/art.png");

        Assert.Equal("/tmp/out.mp4", imported.AbsolutePath);
        Assert.Equal((romPath, "/tmp/in.mp4"), importService.LastPreviewImportArgs);
        Assert.Equal((romPath, "/tmp/cover.png"), importService.LastCoverImportArgs);
        Assert.Equal((romPath, "/tmp/art.png"), importService.LastArtworkImportArgs);
    }

    [Fact]
    public void DeleteRomAssociatedResources_RemovesPreviewArtifacts_RegisteredObjects_Profile_AndTimelineDirectory()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-resource-delete-tests-{Guid.NewGuid():N}");

        try
        {
            var controller = new MainWindowRomAssociatedResourceController();
            var resourceController = new MainWindowResourceManagementController(new RecordingRomResourceImportService());
            resourceController.ConfigureResourceRoot(tempRoot);

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "demo.nes");
            WriteFile(romPath);

            var previewArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                WriteFile($"{AppObjectStorage.GetPreviewArtifactBasePath(romPath)}.mp4"),
                WriteFile($"{AppObjectStorage.GetPreviewArtifactBasePath(romPath)}.fcpv"),
                WriteFile($"{AppObjectStorage.GetLegacyPreviewArtifactBasePath(romPath)}.mov")
            };

            var previewObjectPath = WriteFile(AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, "demo-preview.mp4"));
            var coverObjectPath = WriteFile(AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Images, "demo-cover.png"));
            var exportObjectPath = WriteFile(AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Saves, "demo/export.mp4"));
            var otherObjectPath = WriteFile(AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Other, "misc/demo.bin"));

            RomConfigProfile.Save(romPath, new RomConfigProfile());
            var profilePath = RomConfigProfile.GetProfilePath(romPath);
            Assert.True(File.Exists(profilePath));
            RomConfigProfile.RegisterAdditionalObject(romPath, "preview.video", "demo-preview.mp4");
            RomConfigProfile.RegisterAdditionalObject(romPath, "image.cover", "demo-cover.png");
            RomConfigProfile.RegisterAdditionalObject(romPath, "exports.video.1", "demo/export.mp4");
            RomConfigProfile.RegisterAdditionalObject(romPath, "misc.payload", "misc/demo.bin");

            var romId = TimelineStoragePaths.ComputeRomId(romPath);
            var timelineDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            WriteFile(Path.Combine(timelineDirectory, "manifest.json"));
            Assert.True(Directory.Exists(timelineDirectory));

            controller.DeleteRomAssociatedResources(romPath);

            Assert.All(previewArtifacts, path => Assert.False(File.Exists(path)));
            Assert.False(File.Exists(previewObjectPath));
            Assert.False(File.Exists(coverObjectPath));
            Assert.False(File.Exists(exportObjectPath));
            Assert.False(File.Exists(otherObjectPath));
            Assert.False(File.Exists(profilePath));
            Assert.False(Directory.Exists(timelineDirectory));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ExecuteCleanup_RemovesOnlySelectedTypes_AndPreservesOtherResources()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-resource-selected-cleanup-tests-{Guid.NewGuid():N}");

        try
        {
            var controller = new MainWindowResourceManagementController(new RecordingRomResourceImportService());
            controller.ConfigureResourceRoot(tempRoot);

            var previewFile = WriteFile(Path.Combine(AppObjectStorage.GetPreviewVideosDirectory(), "keep-preview.mp4"));
            var imageFile = WriteFile(Path.Combine(AppObjectStorage.GetImagesDirectory(), "keep-cover.png"));
            var timelineSaveFile = WriteFile(Path.Combine(AppObjectStorage.GetTimelineRootDirectory(), "remove-timeline.bin"));
            var exportVideoFile = WriteFile(Path.Combine(AppObjectStorage.GetTimelineRootDirectory(), "keep-export.mp4"));

            var rom = new RomLibraryItem(
                "demo.nes",
                Path.Combine(tempRoot, "roms", "demo.nes"),
                previewFile,
                hasPreview: true,
                fileSizeBytes: 1024,
                importedAtUtc: DateTime.UtcNow);

            var result = controller.ExecuteCleanup(
                new ResourceCleanupSelection(
                    CleanupPreviewAnimations: false,
                    CleanupThumbnails: false,
                    CleanupTimelineSaves: true,
                    CleanupExportVideos: false),
                [rom]);

            Assert.Equal(0, result.RemovedPreviews);
            Assert.Equal(0, result.RemovedImages);
            Assert.Equal(1, result.RemovedTimelineFiles);
            Assert.Equal(0, result.RemovedExportVideos);

            Assert.True(File.Exists(previewFile));
            Assert.True(File.Exists(imageFile));
            Assert.False(File.Exists(timelineSaveFile));
            Assert.True(File.Exists(exportVideoFile));
            Assert.True(rom.HasPreview);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildRomAssociatedResourceSummary_CountsPreviewCoverArtworkExportsAndTimelineDirectory()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-resource-summary-tests-{Guid.NewGuid():N}");

        try
        {
            var controller = new MainWindowRomAssociatedResourceController();
            var resourceController = new MainWindowResourceManagementController(new RecordingRomResourceImportService());
            resourceController.ConfigureResourceRoot(tempRoot);

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "demo.nes");
            WriteFile(romPath);
            RomConfigProfile.Save(romPath, new RomConfigProfile());

            RomConfigProfile.RegisterAdditionalObject(romPath, "preview.video", "summary-preview.mp4");
            RomConfigProfile.RegisterAdditionalObject(romPath, "image.cover", "summary-cover.png");
            RomConfigProfile.RegisterAdditionalObject(romPath, "image.artwork.1", "summary-art-1.png");
            RomConfigProfile.RegisterAdditionalObject(romPath, "image.artwork.2", "summary-art-2.png");
            RomConfigProfile.RegisterAdditionalObject(romPath, "exports.video.1", "summary-export-1.mp4");
            RomConfigProfile.RegisterAdditionalObject(romPath, "exports.video.2", "summary-export-2.mp4");

            var romId = TimelineStoragePaths.ComputeRomId(romPath);
            var timelineDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            WriteFile(Path.Combine(timelineDirectory, "manifest.json"));

            var summary = controller.BuildRomAssociatedResourceSummary(romPath);

            Assert.Equal(
                "关联资源摘要: 预览视频 1 个，封面图 1 个，附加图片 2 个，导出视频 2 个，存档目录 1 个。",
                summary);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            AppObjectStorage.EnsureDefaults();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string WriteFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, "x");
        return path;
    }

    private sealed class RecordingRomResourceImportService : IRomResourceImportService
    {
        public (string romPath, string sourcePath)? LastPreviewImportArgs { get; private set; }

        public (string romPath, string sourcePath)? LastCoverImportArgs { get; private set; }

        public (string romPath, string sourcePath)? LastArtworkImportArgs { get; private set; }

        public ImportedRomResource ImportRom(string sourcePath) =>
            new("rom", "rom-key", sourcePath);

        public IReadOnlyList<ImportedRomResource> ImportRomDirectory(string directoryPath, bool recursive = true) =>
            [new ImportedRomResource("rom", "rom-key", directoryPath)];

        public ImportedRomResource ImportPreviewVideo(string romPath, string sourcePath)
        {
            LastPreviewImportArgs = (romPath, sourcePath);
            return new ImportedRomResource("preview.video", "preview-key", "/tmp/out.mp4");
        }

        public ImportedRomResource ImportCoverImage(string romPath, string sourcePath)
        {
            LastCoverImportArgs = (romPath, sourcePath);
            return new ImportedRomResource("image.cover", "cover-key", sourcePath);
        }

        public ImportedRomResource ImportArtworkImage(string romPath, string sourcePath)
        {
            LastArtworkImportArgs = (romPath, sourcePath);
            return new ImportedRomResource("image.artwork.1", "art-key", sourcePath);
        }
    }
}
