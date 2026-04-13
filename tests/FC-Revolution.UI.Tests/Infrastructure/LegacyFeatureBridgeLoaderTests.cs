using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

public sealed class LegacyFeatureBridgeLoaderTests
{
    [Fact]
    public void Loader_Resolves_Bridge_Provider_And_Creates_All_Bridges()
    {
        var timelineRepository = LegacyFeatureBridgeLoader.CreateTimelineRepositoryBridge();
        var replayRenderer = LegacyFeatureBridgeLoader.GetReplayFrameRenderer();
        var romMapperInspector = LegacyFeatureBridgeLoader.GetRomMapperInfoInspector();

        Assert.IsAssignableFrom<ITimelineRepositoryBridge>(timelineRepository);
        Assert.IsAssignableFrom<IReplayFrameRenderer>(replayRenderer);
        Assert.IsAssignableFrom<IRomMapperInfoInspector>(romMapperInspector);
    }
}
