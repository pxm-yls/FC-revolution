using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FC_Revolution.UI.Models;
using FCRevolution.Storage;

namespace FC_Revolution.UI.AppServices;

public sealed class RomResourceImportService : IRomResourceImportService
{
    public ImportedRomResource ImportRom(string sourcePath)
    {
        var romBucketRoot = Path.GetFullPath(AppObjectStorage.GetRomsDirectory());
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (fullSourcePath.StartsWith(romBucketRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var existingObjectKey = AppObjectStorage.Default.GetObjectKey(ObjectStorageBucket.Roms, fullSourcePath);
            RomConfigProfile.EnsureResourceManifest(fullSourcePath);
            return new ImportedRomResource("rom.binary", existingObjectKey, fullSourcePath);
        }

        var objectKey = AppObjectStorage.GetRomObjectKey(sourcePath);
        var targetPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Roms, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        RomConfigProfile.EnsureResourceManifest(targetPath);
        return new ImportedRomResource("rom.binary", objectKey, targetPath);
    }

    public IReadOnlyList<ImportedRomResource> ImportRomDirectory(string directoryPath, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(directoryPath, "*.nes", searchOption)
            .Select(ImportRom)
            .ToList();
    }

    public ImportedRomResource ImportPreviewVideo(string romPath, string sourcePath)
    {
        var objectKey = AppObjectStorage.GetPreviewVideoObjectKey(romPath, sourcePath);
        var targetPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        RomConfigProfile.RegisterPreviewVideoObject(romPath, objectKey);
        return new ImportedRomResource("preview.video", objectKey, targetPath);
    }

    public ImportedRomResource ImportCoverImage(string romPath, string sourcePath)
    {
        var objectKey = AppObjectStorage.GetRomImageObjectKey(romPath, "cover", sourcePath);
        var targetPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Images, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        RomConfigProfile.RegisterAdditionalObject(romPath, "image.cover", objectKey);
        return new ImportedRomResource("image.cover", objectKey, targetPath);
    }

    public ImportedRomResource ImportArtworkImage(string romPath, string sourcePath)
    {
        var sourceName = AppObjectStorage.SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        var resourceName = $"image.artwork.{sourceName}".ToLowerInvariant();
        var objectKey = AppObjectStorage.GetRomImageObjectKey(romPath, $"artwork-{sourceName}", sourcePath);
        var targetPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.Images, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        RomConfigProfile.RegisterAdditionalObject(romPath, resourceName, objectKey);
        return new ImportedRomResource(resourceName, objectKey, targetPath);
    }
}
