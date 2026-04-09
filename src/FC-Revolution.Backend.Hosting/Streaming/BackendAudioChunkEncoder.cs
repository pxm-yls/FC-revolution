namespace FCRevolution.Backend.Hosting.Streaming;

internal static class BackendAudioChunkEncoder
{
    private const double ResampleRatio = 48000.0 / 44744.0;
    internal const int OutputSampleRate = 48000;

    internal static int GetPayloadLength(float[] samples)
        => GetResampledSampleCount(samples.Length) * 2;

    internal static void FillPcm16Le(float[] samples, Span<byte> destination)
    {
        var outputSampleCount = GetResampledSampleCount(samples.Length);
        if (destination.Length < outputSampleCount * 2)
            throw new ArgumentException("PCM destination buffer is too small.", nameof(destination));

        for (var i = 0; i < outputSampleCount; i++)
        {
            var position = i / ResampleRatio;
            var index = (int)position;
            var fraction = position - index;
            var a = index < samples.Length ? samples[index] : 0f;
            var b = index + 1 < samples.Length ? samples[index + 1] : 0f;
            var value = (float)(a + fraction * (b - a));
            var sample = (short)Math.Clamp((int)(value * 32767f), short.MinValue, short.MaxValue);
            destination[i * 2] = (byte)(sample & 0xFF);
            destination[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
    }

    private static int GetResampledSampleCount(int sourceLength)
        => Math.Max(0, (int)Math.Round(sourceLength * ResampleRatio));
}
