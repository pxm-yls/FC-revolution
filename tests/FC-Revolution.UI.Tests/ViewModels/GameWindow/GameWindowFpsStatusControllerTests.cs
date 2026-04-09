using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowFpsStatusControllerTests
{
    [Theory]
    [InlineData(0.49, false)]
    [InlineData(0.5, true)]
    [InlineData(0.75, true)]
    public void ShouldUpdate_UsesHalfSecondThreshold(double elapsedSeconds, bool expected)
    {
        var shouldUpdate = GameWindowFpsStatusController.ShouldUpdate(elapsedSeconds);

        Assert.Equal(expected, shouldUpdate);
    }

    [Fact]
    public void BuildStatusText_FormatsAudioNormalStatus()
    {
        var text = GameWindowFpsStatusController.BuildStatusText(
            uiFps: 59.7,
            emuFpsRaw: 60,
            frameTimeMicros: 16666,
            isAudioAvailable: true,
            audioInitError: null);

        Assert.Equal("显示:60 模拟:60 帧耗:16.7ms | 音频:正常", text);
    }

    [Fact]
    public void BuildStatusText_FormatsAudioFailureWithFallbackMessage()
    {
        var text = GameWindowFpsStatusController.BuildStatusText(
            uiFps: 42.2,
            emuFpsRaw: 58,
            frameTimeMicros: 20000,
            isAudioAvailable: false,
            audioInitError: null);

        Assert.Equal("显示:42 模拟:58 帧耗:20.0ms | 音频:失败(无设备)", text);
    }

    [Fact]
    public void BuildStatusText_FormatsAudioFailureWithErrorDetail()
    {
        var text = GameWindowFpsStatusController.BuildStatusText(
            uiFps: 30.4,
            emuFpsRaw: 52,
            frameTimeMicros: 33333,
            isAudioAvailable: false,
            audioInitError: "device busy");

        Assert.Equal("显示:30 模拟:52 帧耗:33.3ms | 音频:失败(device busy)", text);
    }
}
