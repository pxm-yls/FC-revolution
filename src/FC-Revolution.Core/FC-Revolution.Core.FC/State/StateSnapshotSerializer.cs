namespace FCRevolution.Core.State;

/// <summary>Binary serializer for unified emulator snapshots.</summary>
public static class StateSnapshotSerializer
{
    private static ReadOnlySpan<byte> Magic => "FCRS"u8;
    private const byte CurrentVersion = 1;
    private const byte ThumbnailFlag = 0x01;

    public static byte[] Serialize(StateSnapshotData snapshot, bool includeThumbnail)
    {
        var flags = includeThumbnail && snapshot.Thumbnail is { Length: > 0 } ? ThumbnailFlag : (byte)0;
        var thumbnailLength = (flags & ThumbnailFlag) != 0 ? snapshot.Thumbnail!.Length : 0;
        var cpuLength = snapshot.CpuState.Length;
        var ppuLength = snapshot.PpuState.Length;
        var ramLength = snapshot.RamState.Length;
        var cartLength = snapshot.CartState.Length;
        var apuLength = snapshot.ApuState.Length;
        var total = Magic.Length + 1 + 1 + 8 + 8
            + 4 + cpuLength
            + 4 + ppuLength
            + 4 + ramLength
            + 4 + cartLength
            + 4 + apuLength
            + 4 + thumbnailLength * sizeof(uint);
        var buffer = new byte[total];
        var offset = 0;

        Magic.CopyTo(buffer);
        offset += Magic.Length;
        buffer[offset++] = CurrentVersion;
        buffer[offset++] = flags;
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), snapshot.Frame);
        offset += 8;
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), snapshot.Timestamp);
        offset += 8;

        WritePart(snapshot.CpuState, buffer, ref offset);
        WritePart(snapshot.PpuState, buffer, ref offset);
        WritePart(snapshot.RamState, buffer, ref offset);
        WritePart(snapshot.CartState, buffer, ref offset);
        WritePart(snapshot.ApuState, buffer, ref offset);

        BitConverter.TryWriteBytes(buffer.AsSpan(offset), thumbnailLength);
        offset += 4;

        if (thumbnailLength > 0)
            Buffer.BlockCopy(snapshot.Thumbnail!, 0, buffer, offset, thumbnailLength * sizeof(uint));

        return buffer;
    }

    public static StateSnapshotData Deserialize(byte[] data)
    {
        if (!HasHeader(data))
            throw new InvalidDataException("Snapshot does not contain the FCRS header.");

        var offset = Magic.Length;
        var version = data[offset++];
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported snapshot version: {version}.");

        var flags = data[offset++];
        var frame = BitConverter.ToInt64(data, offset);
        offset += 8;
        var timestamp = BitConverter.ToDouble(data, offset);
        offset += 8;

        static byte[] ReadPart(byte[] source, ref int partOffset)
        {
            var length = BitConverter.ToInt32(source, partOffset);
            partOffset += 4;
            var part = new byte[length];
            if (length > 0)
                Buffer.BlockCopy(source, partOffset, part, 0, length);
            partOffset += length;
            return part;
        }

        var cpu = ReadPart(data, ref offset);
        var ppu = ReadPart(data, ref offset);
        var ram = ReadPart(data, ref offset);
        var cart = ReadPart(data, ref offset);
        var apu = ReadPart(data, ref offset);
        var thumbnailLength = BitConverter.ToInt32(data, offset);
        offset += 4;

        uint[]? thumbnail = null;
        if ((flags & ThumbnailFlag) != 0 && thumbnailLength > 0)
        {
            thumbnail = new uint[thumbnailLength];
            Buffer.BlockCopy(data, offset, thumbnail, 0, thumbnailLength * sizeof(uint));
        }

        return new StateSnapshotData
        {
            Frame = frame,
            Timestamp = timestamp,
            CpuState = cpu,
            PpuState = ppu,
            RamState = ram,
            CartState = cart,
            ApuState = apu,
            Thumbnail = thumbnail,
        };
    }

    public static bool HasHeader(ReadOnlySpan<byte> data) => data.Length >= Magic.Length && data[..Magic.Length].SequenceEqual(Magic);

    private static void WritePart(byte[] part, byte[] destination, ref int offset)
    {
        BitConverter.TryWriteBytes(destination.AsSpan(offset), part.Length);
        offset += 4;
        if (part.Length > 0)
        {
            part.CopyTo(destination, offset);
            offset += part.Length;
        }
    }
}
