namespace FCRevolution.Emulation.Abstractions;

public sealed record CoreRomMapperInfo(int Number, string Name, bool IsSupported);

public interface IRomMapperInfoInspector
{
    CoreRomMapperInfo Inspect(string romPath);
}

public interface IReplayFrameRenderer
{
    IReadOnlyList<uint[]> RenderFrameRange(
        string romPath,
        byte[] snapshotBytes,
        string inputLogPath,
        long startFrame,
        long endFrame);
}

public interface ILegacyFeatureBridgeProvider
{
    ITimelineRepositoryBridge CreateTimelineRepositoryBridge();

    IReplayFrameRenderer CreateReplayFrameRenderer();

    IRomMapperInfoInspector CreateRomMapperInfoInspector();
}
