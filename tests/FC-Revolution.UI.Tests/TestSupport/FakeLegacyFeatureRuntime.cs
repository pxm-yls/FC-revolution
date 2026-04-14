using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

internal sealed class FakeLegacyFeatureRuntime : ILegacyFeatureRuntime
{
    public string ErrorMessage { get; set; } = "legacy feature unavailable";

    public ITimelineRepositoryBridge? TimelineRepository { get; set; }

    public IReplayFrameRenderer? ReplayFrameRenderer { get; set; }

    public IRomMapperInfoInspector? RomMapperInfoInspector { get; set; }

    public bool TryCreateTimelineRepositoryBridge(out ITimelineRepositoryBridge timelineRepository, out string? errorMessage)
    {
        if (TimelineRepository is not null)
        {
            timelineRepository = TimelineRepository;
            errorMessage = null;
            return true;
        }

        timelineRepository = null!;
        errorMessage = ErrorMessage;
        return false;
    }

    public bool TryGetReplayFrameRenderer(out IReplayFrameRenderer replayFrameRenderer, out string? errorMessage)
    {
        if (ReplayFrameRenderer is not null)
        {
            replayFrameRenderer = ReplayFrameRenderer;
            errorMessage = null;
            return true;
        }

        replayFrameRenderer = null!;
        errorMessage = ErrorMessage;
        return false;
    }

    public bool TryGetRomMapperInfoInspector(out IRomMapperInfoInspector romMapperInfoInspector, out string? errorMessage)
    {
        if (RomMapperInfoInspector is not null)
        {
            romMapperInfoInspector = RomMapperInfoInspector;
            errorMessage = null;
            return true;
        }

        romMapperInfoInspector = null!;
        errorMessage = ErrorMessage;
        return false;
    }
}
