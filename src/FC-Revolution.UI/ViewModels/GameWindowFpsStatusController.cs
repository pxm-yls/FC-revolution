namespace FC_Revolution.UI.ViewModels;

internal static class GameWindowFpsStatusController
{
    private const double MinFpsWindowSeconds = 0.5;

    public static bool ShouldUpdate(double elapsedSeconds) => elapsedSeconds >= MinFpsWindowSeconds;

    public static string BuildStatusText(
        double uiFps,
        int emuFpsRaw,
        int frameTimeMicros,
        bool isAudioAvailable,
        string? audioInitError)
    {
        var audioTag = isAudioAvailable
            ? "音频:正常"
            : $"音频:失败({audioInitError ?? "无设备"})";
        return $"显示:{uiFps:F0} 模拟:{emuFpsRaw} 帧耗:{frameTimeMicros / 1000.0:F1}ms | {audioTag}";
    }
}
