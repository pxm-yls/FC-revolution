using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.FC.LegacyAdapters;

public sealed class LegacyReplayFrameRenderer : IReplayFrameRenderer
{
    public IReadOnlyList<uint[]> RenderFrameRange(
        string romPath,
        byte[] snapshotBytes,
        string inputLogPath,
        long startFrame,
        long endFrame)
    {
        return NesReplayInterop.RenderFrameRange(
            romPath,
            snapshotBytes,
            inputLogPath,
            startFrame,
            endFrame);
    }
}
