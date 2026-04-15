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

public interface ITimelineRepositoryBridgeProvider
{
    ITimelineRepositoryBridge CreateTimelineRepositoryBridge();
}

public interface IReplayFrameRendererProvider
{
    IReplayFrameRenderer CreateReplayFrameRenderer();
}

public interface IRomMapperInfoInspectorProvider
{
    IRomMapperInfoInspector CreateRomMapperInfoInspector();
}

public interface ILegacyFeatureBridgeProvider :
    ITimelineRepositoryBridgeProvider,
    IReplayFrameRendererProvider,
    IRomMapperInfoInspectorProvider
{
}
