using System;
using System.IO;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewStreamMetadata(
    bool IsAnimated,
    int IntervalMs,
    int FrameCount);

internal sealed record PreviewStreamLoadResult(
    string PlaybackPath,
    bool FileExists,
    StreamingPreview? Preview,
    string? FormatLabel,
    PreviewStreamMetadata? Metadata);

internal sealed class MainWindowPreviewStreamController
{
    private readonly string _previewMagicV1;
    private readonly string _previewMagicV2;

    public MainWindowPreviewStreamController(string previewMagicV1, string previewMagicV2)
    {
        _previewMagicV1 = previewMagicV1;
        _previewMagicV2 = previewMagicV2;
    }

    public PreviewStreamLoadResult LoadPreviewStream(
        string romPath,
        Func<string, string> resolvePlaybackPath,
        Func<string, string> getPreviewPath,
        Action<string, string> upgradeLegacyPreview)
    {
        var playbackPath = resolvePlaybackPath(romPath);
        if (!File.Exists(playbackPath))
            return new PreviewStreamLoadResult(playbackPath, false, null, null, null);

        var preview = StreamingPreview.Open(playbackPath, _previewMagicV1, _previewMagicV2);
        var formatLabel = preview.IsLegacyPreview ? "旧版FCPV1" : Path.GetExtension(playbackPath);
        if (preview.IsLegacyPreview)
        {
            preview.Dispose();
            var migratedPreviewPath = getPreviewPath(romPath);
            upgradeLegacyPreview(playbackPath, migratedPreviewPath);
            playbackPath = migratedPreviewPath;
            preview = StreamingPreview.Open(playbackPath, _previewMagicV1, _previewMagicV2);
            formatLabel = Path.GetExtension(playbackPath);
        }

        var metadata = new PreviewStreamMetadata(preview.IsAnimated, preview.IntervalMs, preview.FrameCount);
        return new PreviewStreamLoadResult(playbackPath, true, preview, formatLabel, metadata);
    }

    public bool ShouldPersistMetadata(
        bool? knownPreviewIsAnimated,
        int knownPreviewIntervalMs,
        int knownPreviewFrameCount,
        PreviewStreamMetadata metadata)
    {
        return !knownPreviewIsAnimated.HasValue ||
               knownPreviewIntervalMs != metadata.IntervalMs ||
               knownPreviewFrameCount != metadata.FrameCount ||
               knownPreviewIsAnimated.Value != metadata.IsAnimated;
    }
}
