using System.Buffers.Binary;

namespace FCRevolution.Storage;

/// <summary>Reads the base frame from unified save-state payloads without taking a dependency on core internals.</summary>
public static class StateSnapshotFrameReader
{
    private static ReadOnlySpan<byte> Magic => "FCRS"u8;
    private const byte CurrentVersion = 1;
    private const int VersionOffset = 4;
    private const int FlagsOffset = 5;
    private const int FrameOffset = 6;
    private const int MinimumHeaderLength = FrameOffset + sizeof(long);

    public static bool HasHeader(ReadOnlySpan<byte> data) => data.Length >= Magic.Length && data[..Magic.Length].SequenceEqual(Magic);

    public static long ReadFrame(ReadOnlySpan<byte> data)
    {
        if (!HasHeader(data))
            throw new InvalidDataException("Snapshot does not contain the FCRS header.");
        if (data.Length < MinimumHeaderLength)
            throw new InvalidDataException("Snapshot frame header is incomplete.");

        var version = data[VersionOffset];
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported snapshot version: {version}.");

        _ = data[FlagsOffset];
        return BinaryPrimitives.ReadInt64LittleEndian(data[FrameOffset..(FrameOffset + sizeof(long))]);
    }
}
