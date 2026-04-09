using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private string GetPreviewDirectory() => AppObjectStorage.GetPreviewVideosDirectory();

    private string GetPreviewPath(string romPath)
    {
        return $"{GetPreviewArtifactBasePath(romPath)}.mp4";
    }

    private string GetLocalPreviewPath(string romPath)
    {
        return $"{GetPreviewArtifactBasePath(romPath)}{LegacyPreviewExtension}";
    }

    private string ResolvePreviewPath(string romPath)
    {
        var registeredPreviewPath = ResolveRegisteredPreviewPath(romPath);
        return _previewAssetController.ResolvePreviewPath(
            romPath,
            registeredPreviewPath,
            GetPreviewArtifactBasePathCandidates(romPath),
            File.Exists,
            GetPreviewPath);
    }

    private string ResolvePreviewPlaybackPath(string romPath)
    {
        var resolvedPreviewPath = ResolvePreviewPath(romPath);
        return _previewAssetController.ResolvePreviewPlaybackPath(
            romPath,
            resolvedPreviewPath,
            GetPreviewArtifactBasePathCandidates(romPath),
            File.Exists,
            GetPreviewPath,
            PathsEqual);
    }

    private static string? ResolveRegisteredPreviewPath(string romPath)
    {
        var profile = RomConfigProfile.Load(romPath);
        if (profile.Resources?.AdditionalObjects.TryGetValue("preview.video", out var objectKey) == true &&
            !string.IsNullOrWhiteSpace(objectKey))
        {
            var absolutePath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, objectKey);
            if (File.Exists(absolutePath))
                return absolutePath;
        }

        if (!string.IsNullOrWhiteSpace(profile.Resources?.PreviewVideoObjectKey))
        {
            var manifestPath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, profile.Resources.PreviewVideoObjectKey);
            if (File.Exists(manifestPath))
                return manifestPath;
        }

        var matchedPath = FindPreviewByRomPrefix(profile);
        if (!string.IsNullOrWhiteSpace(matchedPath))
            return matchedPath;

        return null;
    }

    private static string? FindPreviewByRomPrefix(RomConfigProfile profile)
    {
        var romObjectKey = profile.Resources?.RomObjectKey;
        if (string.IsNullOrWhiteSpace(romObjectKey))
            return null;

        var prefix = Path.GetFileNameWithoutExtension(romObjectKey);
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        return Directory
            .EnumerateFiles(AppObjectStorage.GetPreviewVideosDirectory(), $"{prefix}*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedVideoPreviewExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private IEnumerable<string> EnumeratePreviewArtifacts(string romPath)
    {
        foreach (var artifactBasePath in GetPreviewArtifactBasePathCandidates(romPath))
        {
            yield return $"{artifactBasePath}.mp4";
            yield return $"{artifactBasePath}{LegacyPreviewExtension}";
            foreach (var extension in SupportedVideoPreviewExtensions)
            {
                var videoPath = $"{artifactBasePath}{extension}";
                if (!string.Equals(videoPath, $"{artifactBasePath}.mp4", StringComparison.OrdinalIgnoreCase))
                    yield return videoPath;
            }
        }
    }

    private IEnumerable<string> GetPreviewArtifactBasePathCandidates(string romPath)
    {
        yield return GetPreviewArtifactBasePath(romPath);

        var legacyBasePath = AppObjectStorage.GetLegacyPreviewArtifactBasePath(romPath);
        if (!string.Equals(legacyBasePath, GetPreviewArtifactBasePath(romPath), StringComparison.OrdinalIgnoreCase))
            yield return legacyBasePath;
    }

    private string GetPreviewArtifactBasePath(string romPath)
    {
        return AppObjectStorage.GetPreviewArtifactBasePath(romPath);
    }
}
