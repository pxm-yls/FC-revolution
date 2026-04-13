using System;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class LegacyFeatureBridgeLoader
{
    private const string TimelineRepositoryTypeName = "FCRevolution.FC.LegacyAdapters.LegacyTimelineRepositoryAdapter, FC-Revolution.FC.LegacyAdapters";
    private const string ReplayRendererTypeName = "FCRevolution.FC.LegacyAdapters.LegacyReplayFrameRenderer, FC-Revolution.FC.LegacyAdapters";
    private const string RomMapperInspectorTypeName = "FCRevolution.FC.LegacyAdapters.LegacyRomMapperInspector, FC-Revolution.FC.LegacyAdapters";

    private static readonly Lazy<IReplayFrameRenderer> ReplayFrameRenderer = new(() => CreateInstance<IReplayFrameRenderer>(ReplayRendererTypeName));
    private static readonly Lazy<IRomMapperInfoInspector> RomMapperInfoInspector = new(() => CreateInstance<IRomMapperInfoInspector>(RomMapperInspectorTypeName));

    public static ITimelineRepositoryBridge CreateTimelineRepositoryBridge() =>
        CreateInstance<ITimelineRepositoryBridge>(TimelineRepositoryTypeName);

    public static IReplayFrameRenderer GetReplayFrameRenderer() => ReplayFrameRenderer.Value;

    public static IRomMapperInfoInspector GetRomMapperInfoInspector() => RomMapperInfoInspector.Value;

    private static TBridge CreateInstance<TBridge>(string assemblyQualifiedTypeName)
        where TBridge : class
    {
        var type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
        if (type == null)
        {
            throw new InvalidOperationException(
                $"无法加载 legacy bridge 类型 {assemblyQualifiedTypeName}。请确认 FC-Revolution.FC.LegacyAdapters 已随应用输出。");
        }

        if (Activator.CreateInstance(type) is not TBridge bridge)
        {
            throw new InvalidOperationException(
                $"legacy bridge 类型 {type.FullName} 未实现所需契约 {typeof(TBridge).FullName}。");
        }

        return bridge;
    }
}
