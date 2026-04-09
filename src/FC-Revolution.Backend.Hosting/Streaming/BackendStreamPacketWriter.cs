using System.Buffers;
using System.Buffers.Binary;

namespace FCRevolution.Backend.Hosting.Streaming;

internal static class BackendStreamPacketWriter
{
    internal static (byte[] Buffer, int MessageLength) RentMessage(
        int payloadLength,
        byte messageType,
        byte codec,
        int sequence,
        int metadata)
    {
        var messageLength = BackendStreamProtocol.HeaderSize + payloadLength;
        var message = ArrayPool<byte>.Shared.Rent(messageLength);
        WriteHeader(message, messageType, codec, sequence, metadata);
        return (message, messageLength);
    }

    internal static void CopyPayload(byte[] destination, ReadOnlySpan<byte> payload)
        => payload.CopyTo(destination.AsSpan(BackendStreamProtocol.HeaderSize, payload.Length));

    internal static Span<byte> GetPayloadSpan(byte[] buffer, int payloadLength)
        => buffer.AsSpan(BackendStreamProtocol.HeaderSize, payloadLength);

    internal static void Return(byte[] buffer)
        => ArrayPool<byte>.Shared.Return(buffer);

    private static void WriteHeader(byte[] buffer, byte messageType, byte codec, int sequence, int metadata)
    {
        buffer[0] = messageType;
        buffer[1] = BackendStreamProtocol.Version;
        buffer[2] = codec;
        buffer[3] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), metadata);
    }
}
