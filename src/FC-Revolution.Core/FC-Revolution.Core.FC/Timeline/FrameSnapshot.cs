namespace FCRevolution.Core.Timeline;

/// <summary>Represents a complete emulator state at a specific frame boundary.</summary>
public sealed class FrameSnapshot
{
    public long   Frame     { get; init; }
    public double Timestamp { get; init; }  // seconds from session start
    public byte[] CpuState  { get; init; } = Array.Empty<byte>();
    public byte[] PpuState  { get; init; } = Array.Empty<byte>();
    public byte[] RamState  { get; init; } = Array.Empty<byte>();
    public byte[] CartState { get; init; } = Array.Empty<byte>();
    public byte[] ApuState  { get; init; } = Array.Empty<byte>();

    /// <summary>Preview thumbnail (64×60 ARGB, downsampled from 256×240).</summary>
    public uint[] Thumbnail { get; init; } = new uint[64 * 60];

    public static uint[] MakeThumbnail(uint[] fb)
    {
        var thumb = new uint[64 * 60];
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 64; x++)
        {
            int srcX = x * 4, srcY = y * 4;
            thumb[y * 64 + x] = fb[srcY * 256 + srcX];
        }
        return thumb;
    }
}
