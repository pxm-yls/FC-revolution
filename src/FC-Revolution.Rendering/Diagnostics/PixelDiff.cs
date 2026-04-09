namespace FCRevolution.Rendering.Diagnostics;

public static class PixelDiff
{
    public static float Compare(
        ReadOnlySpan<uint> referenceFrame,
        ReadOnlySpan<uint> candidateFrame,
        byte channelTolerance = 0)
    {
        ValidateLengths(referenceFrame, candidateFrame);

        if (referenceFrame.Length == 0)
            return 0f;

        int mismatchCount = 0;
        for (int i = 0; i < referenceFrame.Length; i++)
        {
            if (PixelsDiffer(referenceFrame[i], candidateFrame[i], channelTolerance))
                mismatchCount++;
        }

        return (float)mismatchCount / referenceFrame.Length;
    }

    public static uint[] BuildHeatmap(
        ReadOnlySpan<uint> referenceFrame,
        ReadOnlySpan<uint> candidateFrame)
    {
        ValidateLengths(referenceFrame, candidateFrame);

        var heatmap = new uint[referenceFrame.Length];
        for (int i = 0; i < referenceFrame.Length; i++)
        {
            uint expected = referenceFrame[i];
            uint actual = candidateFrame[i];
            int redDelta = Math.Abs((int)((expected >> 16) & 0xFF) - (int)((actual >> 16) & 0xFF));
            int greenDelta = Math.Abs((int)((expected >> 8) & 0xFF) - (int)((actual >> 8) & 0xFF));
            int blueDelta = Math.Abs((int)(expected & 0xFF) - (int)(actual & 0xFF));
            byte intensity = (byte)Math.Max(redDelta, Math.Max(greenDelta, blueDelta));
            heatmap[i] = intensity == 0 ? 0xFF000000u : 0xFF000000u | ((uint)intensity << 16);
        }

        return heatmap;
    }

    private static bool PixelsDiffer(uint expected, uint actual, byte channelTolerance)
    {
        return ChannelDiff(expected >> 24, actual >> 24) > channelTolerance ||
               ChannelDiff(expected >> 16, actual >> 16) > channelTolerance ||
               ChannelDiff(expected >> 8, actual >> 8) > channelTolerance ||
               ChannelDiff(expected, actual) > channelTolerance;
    }

    private static byte ChannelDiff(uint expected, uint actual)
        => (byte)Math.Abs((int)(expected & 0xFF) - (int)(actual & 0xFF));

    private static void ValidateLengths(ReadOnlySpan<uint> referenceFrame, ReadOnlySpan<uint> candidateFrame)
    {
        if (referenceFrame.Length != candidateFrame.Length)
        {
            throw new ArgumentException(
                $"Frame lengths must match. Reference={referenceFrame.Length}, Candidate={candidateFrame.Length}.");
        }
    }
}
