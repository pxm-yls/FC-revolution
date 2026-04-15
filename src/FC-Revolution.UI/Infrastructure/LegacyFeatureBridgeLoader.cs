using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class LegacyFeatureBridgeLoader
{
    private static readonly Lazy<LegacyFeatureProviderLoadState<ITimelineRepositoryBridgeProvider>> TimelineRepositoryProvider =
        new(LoadTimelineRepositoryProvider);
    private static readonly Lazy<LegacyFeatureProviderLoadState<IReplayFrameRendererProvider>> ReplayFrameRendererProvider =
        new(LoadReplayFrameRendererProvider);
    private static readonly Lazy<LegacyFeatureProviderLoadState<IRomMapperInfoInspectorProvider>> RomMapperInfoInspectorProvider =
        new(LoadRomMapperInfoInspectorProvider);

    public static ITimelineRepositoryBridge CreateTimelineRepositoryBridge() =>
        TryCreateTimelineRepositoryBridge(out var timelineRepository, out var errorMessage)
            ? timelineRepository
            : throw new InvalidOperationException(errorMessage);

    public static IReplayFrameRenderer GetReplayFrameRenderer() =>
        TryGetReplayFrameRenderer(out var replayFrameRenderer, out var errorMessage)
            ? replayFrameRenderer
            : throw new InvalidOperationException(errorMessage);

    public static IRomMapperInfoInspector GetRomMapperInfoInspector() =>
        TryGetRomMapperInfoInspector(out var romMapperInfoInspector, out var errorMessage)
            ? romMapperInfoInspector
            : throw new InvalidOperationException(errorMessage);

    public static bool TryCreateTimelineRepositoryBridge(
        out ITimelineRepositoryBridge timelineRepository,
        out string? errorMessage)
    {
        timelineRepository = null!;
        if (!TryGetProvider(TimelineRepositoryProvider, out var provider, out errorMessage))
            return false;

        timelineRepository = provider.CreateTimelineRepositoryBridge();
        return true;
    }

    public static bool TryGetReplayFrameRenderer(
        out IReplayFrameRenderer replayFrameRenderer,
        out string? errorMessage)
    {
        replayFrameRenderer = null!;
        if (!TryGetProvider(ReplayFrameRendererProvider, out var provider, out errorMessage))
            return false;

        replayFrameRenderer = provider.CreateReplayFrameRenderer();
        return true;
    }

    public static bool TryGetRomMapperInfoInspector(
        out IRomMapperInfoInspector romMapperInfoInspector,
        out string? errorMessage)
    {
        romMapperInfoInspector = null!;
        if (!TryGetProvider(RomMapperInfoInspectorProvider, out var provider, out errorMessage))
            return false;

        romMapperInfoInspector = provider.CreateRomMapperInfoInspector();
        return true;
    }

    private static bool TryGetProvider<TProvider>(
        Lazy<LegacyFeatureProviderLoadState<TProvider>> loadStateSource,
        out TProvider provider,
        out string? errorMessage)
        where TProvider : class
    {
        var loadState = loadStateSource.Value;
        if (loadState.Provider is not null)
        {
            provider = loadState.Provider;
            errorMessage = null;
            return true;
        }

        provider = null!;
        errorMessage = loadState.ErrorMessage;
        return false;
    }

    private static LegacyFeatureProviderLoadState<ITimelineRepositoryBridgeProvider> LoadTimelineRepositoryProvider() =>
        LoadProvider<ITimelineRepositoryBridgeProvider>("timeline repository bridge");

    private static LegacyFeatureProviderLoadState<IReplayFrameRendererProvider> LoadReplayFrameRendererProvider() =>
        LoadProvider<IReplayFrameRendererProvider>("replay frame renderer");

    private static LegacyFeatureProviderLoadState<IRomMapperInfoInspectorProvider> LoadRomMapperInfoInspectorProvider() =>
        LoadProvider<IRomMapperInfoInspectorProvider>("ROM mapper inspector");

    private static LegacyFeatureProviderLoadState<TProvider> LoadProvider<TProvider>(string capabilityDisplayName)
        where TProvider : class
    {
        try
        {
            var providerTypes = EnumerateCandidateAssemblies()
                .SelectMany(GetProviderTypes<TProvider>)
                .Distinct()
                .ToList();

            if (providerTypes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"无法加载 {capabilityDisplayName} provider。请确认提供该能力的程序集已随应用输出。");
            }

            if (providerTypes.Count > 1)
            {
                throw new InvalidOperationException(
                    $"检测到多个 {capabilityDisplayName} provider 实现: {string.Join(", ", providerTypes.Select(type => type.FullName))}。当前 UI 只能为该能力装载一个 provider。");
            }

            var providerType = providerTypes[0];
            if (Activator.CreateInstance(providerType) is not TProvider provider)
            {
                throw new InvalidOperationException(
                    $"legacy feature provider 类型 {providerType.FullName} 未实现所需契约 {typeof(TProvider).FullName}。");
            }

            return LegacyFeatureProviderLoadState<TProvider>.Success(provider);
        }
        catch (Exception exception)
        {
            return LegacyFeatureProviderLoadState<TProvider>.Fail(exception.Message);
        }
    }

    private static IEnumerable<Assembly> EnumerateCandidateAssemblies()
    {
        var assembliesByName = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => (Assembly: assembly, Name: assembly.GetName().Name))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name))
            .GroupBy(candidate => candidate.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Assembly, StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assembliesByName.Values)
            yield return assembly;

        var baseDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
            yield break;

        foreach (var assemblyPath in Directory.EnumerateFiles(baseDirectory, "FC-Revolution*.dll", SearchOption.TopDirectoryOnly))
        {
            AssemblyName assemblyName;
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(assemblyName.Name) ||
                assembliesByName.ContainsKey(assemblyName.Name))
            {
                continue;
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                continue;
            }

            assembliesByName[assembly.GetName().Name!] = assembly;
            yield return assembly;
        }
    }

    private static IEnumerable<Type> GetProviderTypes<TProvider>(Assembly assembly)
        where TProvider : class
    {
        Type[] candidateTypes;
        try
        {
            candidateTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            candidateTypes = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var type in candidateTypes)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (!typeof(TProvider).IsAssignableFrom(type))
                continue;

            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            yield return type;
        }
    }

    private sealed record LegacyFeatureProviderLoadState<TProvider>(
        TProvider? Provider,
        string? ErrorMessage)
        where TProvider : class
    {
        public static LegacyFeatureProviderLoadState<TProvider> Success(TProvider provider) =>
            new(provider, null);

        public static LegacyFeatureProviderLoadState<TProvider> Fail(string errorMessage) =>
            new(null, errorMessage);
    }
}
