using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class LegacyFeatureBridgeLoader
{
    private static readonly Lazy<ILegacyFeatureBridgeProvider> BridgeProvider = new(LoadProvider);
    private static readonly Lazy<IReplayFrameRenderer> ReplayFrameRenderer = new(() => BridgeProvider.Value.CreateReplayFrameRenderer());
    private static readonly Lazy<IRomMapperInfoInspector> RomMapperInfoInspector = new(() => BridgeProvider.Value.CreateRomMapperInfoInspector());

    public static ITimelineRepositoryBridge CreateTimelineRepositoryBridge() =>
        BridgeProvider.Value.CreateTimelineRepositoryBridge();

    public static IReplayFrameRenderer GetReplayFrameRenderer() => ReplayFrameRenderer.Value;

    public static IRomMapperInfoInspector GetRomMapperInfoInspector() => RomMapperInfoInspector.Value;

    private static ILegacyFeatureBridgeProvider LoadProvider()
    {
        var providerTypes = EnumerateCandidateAssemblies()
            .SelectMany(GetProviderTypes)
            .Distinct()
            .ToList();

        if (providerTypes.Count == 0)
        {
            throw new InvalidOperationException(
                $"无法加载任何 {nameof(ILegacyFeatureBridgeProvider)} 实现。请确认 legacy feature provider 程序集已随应用输出。");
        }

        if (providerTypes.Count > 1)
        {
            throw new InvalidOperationException(
                $"检测到多个 {nameof(ILegacyFeatureBridgeProvider)} 实现: {string.Join(", ", providerTypes.Select(type => type.FullName))}。当前 UI 只能装载一个 legacy feature provider。");
        }

        var providerType = providerTypes[0];
        if (Activator.CreateInstance(providerType) is not ILegacyFeatureBridgeProvider provider)
        {
            throw new InvalidOperationException(
                $"legacy feature provider 类型 {providerType.FullName} 未实现所需契约 {typeof(ILegacyFeatureBridgeProvider).FullName}。");
        }

        return provider;
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

    private static IEnumerable<Type> GetProviderTypes(Assembly assembly)
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

            if (!typeof(ILegacyFeatureBridgeProvider).IsAssignableFrom(type))
                continue;

            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            yield return type;
        }
    }
}
