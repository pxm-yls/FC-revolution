namespace FCRevolution.Backend.Hosting.Streaming;

internal static class BackendStreamProtocol
{
    internal const byte MessageTypeVideo = 0x01;
    internal const byte MessageTypeAudio = 0x02;
    internal const byte Version = 0x01;
    internal const byte CodecJpeg = 0x01;
    internal const byte CodecPcm16Mono = 0x01;
    internal const int HeaderSize = 12;
}
