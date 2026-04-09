using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewAssetController
{
    private readonly IReadOnlyList<string> _supportedVideoPreviewExtensions;
    private readonly string _legacyPreviewExtension;

    public MainWindowPreviewAssetController(
        IReadOnlyList<string> supportedVideoPreviewExtensions,
        string legacyPreviewExtension)
    {
        _supportedVideoPreviewExtensions = supportedVideoPreviewExtensions;
        _legacyPreviewExtension = legacyPreviewExtension;
    }

    public string ResolvePreviewPath(
        string romPath,
        string? registeredPreviewPath,
        IEnumerable<string> artifactBasePathCandidates,
        Func<string, bool> fileExists,
        Func<string, string> getPreviewPath)
    {
        if (!string.IsNullOrWhiteSpace(registeredPreviewPath))
            return registeredPreviewPath;

        foreach (var artifactBasePath in artifactBasePathCandidates)
        {
            foreach (var extension in _supportedVideoPreviewExtensions)
            {
                var videoPath = $"{artifactBasePath}{extension}";
                if (fileExists(videoPath))
                    return videoPath;
            }

            var legacyPath = $"{artifactBasePath}{_legacyPreviewExtension}";
            if (fileExists(legacyPath))
                return legacyPath;
        }

        return getPreviewPath(romPath);
    }

    public string ResolvePreviewPlaybackPath(
        string romPath,
        string resolvedPreviewPath,
        IEnumerable<string> artifactBasePathCandidates,
        Func<string, bool> fileExists,
        Func<string, string> getPreviewPath,
        Func<string, string, bool> pathsEqual)
    {
        if (IsSupportedVideoPreviewPath(resolvedPreviewPath))
        {
            if (fileExists(resolvedPreviewPath))
                TryDeleteSiblingLegacyPreviewFile(resolvedPreviewPath, _legacyPreviewExtension);

            return resolvedPreviewPath;
        }

        if (!IsGeneratedPreviewArtifactPath(romPath, resolvedPreviewPath, artifactBasePathCandidates, pathsEqual))
            return resolvedPreviewPath;

        return getPreviewPath(romPath);
    }

    public bool IsSupportedVideoPreviewPath(string previewPath)
    {
        var ext = Path.GetExtension(previewPath);
        return _supportedVideoPreviewExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsGeneratedPreviewArtifactPath(
        string romPath,
        string previewPath,
        IEnumerable<string> artifactBasePathCandidates,
        Func<string, string, bool> pathsEqual)
    {
        foreach (var artifactBasePath in artifactBasePathCandidates)
        {
            if (pathsEqual($"{artifactBasePath}{_legacyPreviewExtension}", previewPath))
                return true;

            foreach (var extension in _supportedVideoPreviewExtensions)
            {
                if (pathsEqual($"{artifactBasePath}{extension}", previewPath))
                    return true;
            }
        }

        return false;
    }

    public static void TryDeleteSiblingLegacyPreviewFile(string videoPreviewPath, string legacyPreviewExtension)
    {
        var legacyPreviewPath = Path.ChangeExtension(videoPreviewPath, legacyPreviewExtension.TrimStart('.'));
        TryDeletePreviewFile(legacyPreviewPath);
    }

    public static void TryDeletePreviewFile(string previewPath)
    {
        try
        {
            if (File.Exists(previewPath))
                File.Delete(previewPath);
        }
        catch
        {
        }
    }
}
