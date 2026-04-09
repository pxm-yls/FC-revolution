using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowRomAssociatedResourceController
{
    public void DeleteRomAssociatedResources(string romPath)
    {
        var additionalObjects = RomConfigProfile.GetAdditionalObjects(romPath).ToList();

        foreach (var previewPath in EnumeratePreviewArtifacts(romPath).Where(File.Exists))
            File.Delete(previewPath);

        foreach (var entry in additionalObjects)
            DeleteRegisteredObject(entry.Key, entry.Value);

        var profilePath = RomConfigProfile.GetProfilePath(romPath);
        if (File.Exists(profilePath))
            File.Delete(profilePath);

        var legacyProfilePath = AppObjectStorage.GetLegacyRomProfilePath(romPath);
        if (!string.Equals(legacyProfilePath, profilePath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(legacyProfilePath))
        {
            File.Delete(legacyProfilePath);
        }

        try
        {
            var romId = TimelineStoragePaths.ComputeRomId(romPath);
            var timelineDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(timelineDirectory))
                Directory.Delete(timelineDirectory, recursive: true);
        }
        catch
        {
        }
    }

    public string BuildRomAssociatedResourceSummary(string romPath)
    {
        var additionalObjects = RomConfigProfile.GetAdditionalObjects(romPath);
        var previewCount = additionalObjects.Keys.Count(name => string.Equals(name, "preview.video", StringComparison.OrdinalIgnoreCase));
        var coverCount = additionalObjects.Keys.Count(name => string.Equals(name, "image.cover", StringComparison.OrdinalIgnoreCase));
        var artworkCount = additionalObjects.Keys.Count(name => name.StartsWith("image.artwork.", StringComparison.OrdinalIgnoreCase));
        var exportCount = additionalObjects.Keys.Count(name => name.StartsWith("exports.", StringComparison.OrdinalIgnoreCase));

        var timelineCount = 0;
        try
        {
            var romId = TimelineStoragePaths.ComputeRomId(romPath);
            timelineCount = Directory.Exists(TimelineStoragePaths.GetRomDirectory(romId)) ? 1 : 0;
        }
        catch
        {
            timelineCount = 0;
        }

        return $"关联资源摘要: 预览视频 {previewCount} 个，封面图 {coverCount} 个，附加图片 {artworkCount} 个，导出视频 {exportCount} 个，存档目录 {timelineCount} 个。";
    }

    private static void DeleteRegisteredObject(string resourceName, string objectKey)
    {
        var bucket = resourceName switch
        {
            "preview.video" => ObjectStorageBucket.PreviewVideos,
            var name when name.StartsWith("image.", StringComparison.OrdinalIgnoreCase) => ObjectStorageBucket.Images,
            var name when name.StartsWith("exports.", StringComparison.OrdinalIgnoreCase) => ObjectStorageBucket.Saves,
            _ => ObjectStorageBucket.Other
        };

        var absolutePath = AppObjectStorage.Default.GetObjectPath(bucket, objectKey);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }

    private static IEnumerable<string> EnumeratePreviewArtifacts(string romPath)
    {
        foreach (var artifactBasePath in GetPreviewArtifactBasePathCandidates(romPath))
        {
            yield return $"{artifactBasePath}.mp4";
            yield return $"{artifactBasePath}.fcpv";
            yield return $"{artifactBasePath}.mov";
            yield return $"{artifactBasePath}.m4v";
            yield return $"{artifactBasePath}.webm";
        }
    }

    private static IEnumerable<string> GetPreviewArtifactBasePathCandidates(string romPath)
    {
        var currentBasePath = AppObjectStorage.GetPreviewArtifactBasePath(romPath);
        yield return currentBasePath;

        var legacyBasePath = AppObjectStorage.GetLegacyPreviewArtifactBasePath(romPath);
        if (!string.Equals(legacyBasePath, currentBasePath, StringComparison.OrdinalIgnoreCase))
            yield return legacyBasePath;
    }
}
