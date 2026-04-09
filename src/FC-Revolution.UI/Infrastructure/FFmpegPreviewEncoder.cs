using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Infrastructure;

internal static class FFmpegPreviewEncoder
{
    internal enum PreviewEncodingPlatform
    {
        Other = 0,
        MacOS = 1,
        Windows = 2
    }

    internal readonly record struct EncoderAttempt(string Encoder, IReadOnlyList<string> ExtraArgs);

    public static void EncodeMp4(string outputPath, int width, int height, int intervalMs, IReadOnlyList<uint[]> frames, PreviewEncodingMode encodingMode = PreviewEncodingMode.Auto)
    {
        if (frames.Count == 0)
            throw new InvalidOperationException("没有可编码的预览帧。");

        EncodeMp4(outputPath, width, height, intervalMs, stdin =>
        {
            var frameBytes = new byte[width * height * 4];
            foreach (var frame in frames)
            {
                if (frame.Length != width * height)
                    throw new InvalidDataException("预览帧尺寸不匹配。");

                Buffer.BlockCopy(frame, 0, frameBytes, 0, frameBytes.Length);
                stdin.Write(frameBytes, 0, frameBytes.Length);
            }
        }, encodingMode);
    }

    public static void EncodeMp4(string outputPath, int width, int height, int intervalMs, Action<Stream> frameWriter, PreviewEncodingMode encodingMode = PreviewEncodingMode.Auto)
    {
        if (frameWriter == null)
            throw new ArgumentNullException(nameof(frameWriter));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var ffmpegPath = FFmpegRuntimeBootstrap.GetBundledToolPath();
        var fps = Math.Max(1, (int)Math.Round(1000d / Math.Max(1, intervalMs)));
        var tempOutputPath = $"{outputPath}.encoding.mp4";
        var encoderAttempts = GetEncoderAttempts(tempOutputPath, width, height, fps, encodingMode);
        if (File.Exists(tempOutputPath))
            File.Delete(tempOutputPath);

        Exception? lastError = null;
        foreach (var args in encoderAttempts)
        {
            try
            {
                EncodeWithTool(ffmpegPath, args, tempOutputPath, frameWriter);
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                File.Move(tempOutputPath, outputPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (File.Exists(tempOutputPath))
                    File.Delete(tempOutputPath);
            }
        }

        throw new InvalidOperationException($"预览视频编码失败: {lastError?.Message ?? "未知错误"}");
    }

    private static IEnumerable<string[]> GetEncoderAttempts(string outputPath, int width, int height, int fps, PreviewEncodingMode encodingMode)
    {
        foreach (var attempt in GetEncoderAttemptsForPlatform(GetCurrentPlatform(), width, height, encodingMode))
            yield return BuildEncoderArgs(outputPath, width, height, fps, attempt.Encoder, attempt.ExtraArgs);
    }

    internal static IReadOnlyList<EncoderAttempt> GetEncoderAttemptsForPlatform(
        PreviewEncodingPlatform platform,
        int width,
        int height,
        PreviewEncodingMode encodingMode = PreviewEncodingMode.Auto)
    {
        if (encodingMode == PreviewEncodingMode.Software)
            return [new EncoderAttempt("mpeg4", ["-q:v", "5"])];

        var attempts = new List<EncoderAttempt>();
        var hardwareArgs = BuildHardwareEncoderArgs(width, height);

        switch (platform)
        {
            case PreviewEncodingPlatform.MacOS:
                attempts.Add(new EncoderAttempt("h264_videotoolbox", hardwareArgs));
                break;
            case PreviewEncodingPlatform.Windows:
                attempts.Add(new EncoderAttempt("h264_nvenc", hardwareArgs));
                attempts.Add(new EncoderAttempt("h264_qsv", hardwareArgs));
                attempts.Add(new EncoderAttempt("h264_amf", hardwareArgs));
                break;
        }

        // Always keep the current software path as the final compatibility fallback.
        attempts.Add(new EncoderAttempt("mpeg4", ["-q:v", "5"]));
        return attempts;
    }

    private static PreviewEncodingPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PreviewEncodingPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PreviewEncodingPlatform.MacOS;

        return PreviewEncodingPlatform.Other;
    }

    private static IReadOnlyList<string> BuildHardwareEncoderArgs(int width, int height)
    {
        var bitrateKbps = GetHardwareBitrateKbps(width, height);
        return
        [
            "-b:v", $"{bitrateKbps}k",
            "-maxrate", $"{bitrateKbps}k",
            "-bufsize", $"{bitrateKbps * 2}k"
        ];
    }

    private static int GetHardwareBitrateKbps(int width, int height)
    {
        var pixels = width * height;
        if (pixels <= 256 * 240)
            return 1800;

        if (pixels <= 512 * 480)
            return 4000;

        return 8000;
    }

    private static string[] BuildEncoderArgs(string outputPath, int width, int height, int fps, string encoder, IReadOnlyList<string> extraArgs)
    {
        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-f", "rawvideo",
            "-pix_fmt", "bgra",
            "-video_size", $"{width}x{height}",
            "-framerate", fps.ToString(),
            "-i", "pipe:0",
            "-an",
            "-c:v", encoder
        };

        args.AddRange(extraArgs);
        args.AddRange(
        [
            "-g", "1",
            "-bf", "0",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
            outputPath
        ]);
        return args.ToArray();
    }

    private static void EncodeWithTool(string ffmpegPath, string[] args, string outputPath, Action<Stream> frameWriter)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("启动 FFmpeg 工具失败。");
        try
        {
            using var stdin = process.StandardInput.BaseStream;
            frameWriter(stdin);
            stdin.Flush();
            stdin.Close();
            process.WaitForExit();
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw;
        }

        if (process.ExitCode == 0 && File.Exists(outputPath))
            return;

        var errorText = process.StandardError.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(errorText))
            errorText = $"FFmpeg 退出码 {process.ExitCode}";
        throw new InvalidOperationException(errorText);
    }
}
