using System;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal interface ILegacyFeatureRuntime
{
    bool TryCreateTimelineRepositoryBridge(out ITimelineRepositoryBridge timelineRepository, out string? errorMessage);

    bool TryGetReplayFrameRenderer(out IReplayFrameRenderer replayFrameRenderer, out string? errorMessage);

    bool TryGetRomMapperInfoInspector(out IRomMapperInfoInspector romMapperInfoInspector, out string? errorMessage);
}

internal static class LegacyFeatureRuntime
{
    private static ILegacyFeatureRuntime _current = new LoaderBackedLegacyFeatureRuntime();

    public static ILegacyFeatureRuntime Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    private sealed class LoaderBackedLegacyFeatureRuntime : ILegacyFeatureRuntime
    {
        public bool TryCreateTimelineRepositoryBridge(out ITimelineRepositoryBridge timelineRepository, out string? errorMessage) =>
            LegacyFeatureBridgeLoader.TryCreateTimelineRepositoryBridge(out timelineRepository, out errorMessage);

        public bool TryGetReplayFrameRenderer(out IReplayFrameRenderer replayFrameRenderer, out string? errorMessage) =>
            LegacyFeatureBridgeLoader.TryGetReplayFrameRenderer(out replayFrameRenderer, out errorMessage);

        public bool TryGetRomMapperInfoInspector(out IRomMapperInfoInspector romMapperInfoInspector, out string? errorMessage) =>
            LegacyFeatureBridgeLoader.TryGetRomMapperInfoInspector(out romMapperInfoInspector, out errorMessage);
    }
}
