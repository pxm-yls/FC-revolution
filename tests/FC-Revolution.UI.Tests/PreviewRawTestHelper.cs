using System.Text;

namespace FC_Revolution.UI.Tests;

internal static class PreviewRawTestHelper
{
    internal const string PreviewMagicV1 = "FCPV1";
    internal const string PreviewMagicV2 = "FCPV2";

    internal static uint[] CreateSolidFrame(int width, int height, uint pixel)
    {
        var frame = new uint[width * height];
        Array.Fill(frame, pixel);
        return frame;
    }

    internal static void WriteRawPreview(
        string previewPath,
        int width,
        int height,
        int intervalMs,
        IReadOnlyList<uint[]> frames,
        string magic = PreviewMagicV2)
    {
        var frameByteLength = checked(width * height * sizeof(uint));
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);

        using var stream = File.Create(previewPath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes(magic));
        writer.Write(width);
        writer.Write(height);
        writer.Write(intervalMs);
        writer.Write(frames.Count);
        for (var index = 0; index < frames.Count; index++)
            writer.Write((long)index * frameByteLength);

        var frameBytes = new byte[frameByteLength];
        foreach (var frame in frames)
        {
            Buffer.BlockCopy(frame, 0, frameBytes, 0, frameByteLength);
            writer.Write(frameBytes);
        }
    }
}
