using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

public class FFmpegPreviewEncoderTests
{
    [Fact]
    public void GetEncoderAttemptsForPlatform_MacOs_PrefersVideoToolboxThenFallsBackToMpeg4()
    {
        var attempts = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
            FFmpegPreviewEncoder.PreviewEncodingPlatform.MacOS,
            256,
            240);

        Assert.Equal(["h264_videotoolbox", "mpeg4"], attempts.Select(static attempt => attempt.Encoder).ToArray());
    }

    [Fact]
    public void GetEncoderAttemptsForPlatform_Windows_PrefersVendorHardwareEncodersBeforeMpeg4()
    {
        var attempts = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
            FFmpegPreviewEncoder.PreviewEncodingPlatform.Windows,
            256,
            240);

        Assert.Equal(
            ["h264_nvenc", "h264_qsv", "h264_amf", "mpeg4"],
            attempts.Select(static attempt => attempt.Encoder).ToArray());
    }

    [Fact]
    public void GetEncoderAttemptsForPlatform_Other_UsesMpeg4Only()
    {
        var attempts = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
            FFmpegPreviewEncoder.PreviewEncodingPlatform.Other,
            256,
            240);

        var attempt = Assert.Single(attempts);
        Assert.Equal("mpeg4", attempt.Encoder);
        Assert.Equal(["-q:v", "5"], attempt.ExtraArgs);
    }

    [Fact]
    public void GetEncoderAttemptsForPlatform_SoftwareMode_SkipsHardwareEncoders()
    {
        var attempts = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
            FFmpegPreviewEncoder.PreviewEncodingPlatform.Windows,
            256,
            240,
            PreviewEncodingMode.Software);

        var attempt = Assert.Single(attempts);
        Assert.Equal("mpeg4", attempt.Encoder);
    }

    [Fact]
    public void GetEncoderAttemptsForPlatform_HigherResolution_IncreasesHardwareBitrate()
    {
        var smallAttempt = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
                FFmpegPreviewEncoder.PreviewEncodingPlatform.MacOS,
                256,
                240)
            .First();
        var largeAttempt = FFmpegPreviewEncoder.GetEncoderAttemptsForPlatform(
                FFmpegPreviewEncoder.PreviewEncodingPlatform.MacOS,
                768,
                720)
            .First();

        Assert.Contains("1800k", smallAttempt.ExtraArgs);
        Assert.Contains("8000k", largeAttempt.ExtraArgs);
    }
}
