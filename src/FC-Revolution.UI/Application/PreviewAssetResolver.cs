using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal sealed class PreviewAssetResolver
{
    public BackendMediaAsset? ResolveRomPreviewAsset(
        IReadOnlyList<RomLibraryItem> romLibrary,
        string romPath,
        Func<string, string, bool> pathsEqual)
    {
        var rom = romLibrary.FirstOrDefault(item => pathsEqual(item.Path, romPath));
        if (rom == null)
            return null;

        var previewPath = ResolveRomPreviewAssetPath(rom.Path, rom.PreviewFilePath);
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
            return null;

        var contentType = GetPreviewContentType(previewPath);
        return contentType == null ? null : new BackendMediaAsset(previewPath, contentType);
    }

    public string? ResolveRomPreviewAssetPath(string romPath, string fallbackPath)
    {
        var profile = RomConfigProfile.Load(romPath);
        if (profile.Resources?.AdditionalObjects.TryGetValue("preview.video", out var objectKey) == true &&
            !string.IsNullOrWhiteSpace(objectKey))
        {
            var objectPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, objectKey);
            if (File.Exists(objectPath))
                return objectPath;
        }

        if (!string.IsNullOrWhiteSpace(profile.Resources?.PreviewVideoObjectKey))
        {
            var manifestPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, profile.Resources.PreviewVideoObjectKey);
            if (File.Exists(manifestPath))
                return manifestPath;
        }

        var romObjectKey = profile.Resources?.RomObjectKey;
        if (!string.IsNullOrWhiteSpace(romObjectKey))
        {
            var prefix = Path.GetFileNameWithoutExtension(romObjectKey);
            var matchedPath = Directory
                .EnumerateFiles(AppObjectStorage.GetPreviewVideosDirectory(), $"{prefix}*", SearchOption.TopDirectoryOnly)
                .Where(path => GetPreviewContentType(path) != null)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(matchedPath))
                return matchedPath;
        }

        return fallbackPath;
    }

    public string? GetPreviewContentType(string previewPath)
    {
        return Path.GetExtension(previewPath).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".m4v" => "video/x-m4v",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            _ => null
        };
    }
}
