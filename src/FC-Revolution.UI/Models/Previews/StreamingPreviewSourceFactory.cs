using System;
using System.IO;

namespace FC_Revolution.UI.Models.Previews;

internal static class StreamingPreviewSourceFactory
{
    public static IPreviewSource OpenSource(string previewPath, string previewMagicV1, string previewMagicV2)
    {
        return LooksLikeVideoPreview(previewPath)
            ? new FFmpegVideoPreviewSource(previewPath)
            : RawFramePreviewSource.Open(previewPath, previewMagicV1, previewMagicV2);
    }

    private static bool LooksLikeVideoPreview(string previewPath)
    {
        var extension = Path.GetExtension(previewPath);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }
}
