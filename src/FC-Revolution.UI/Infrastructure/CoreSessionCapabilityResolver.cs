using System;
using FCRevolution.Core;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class CoreSessionCapabilityResolver
{
    public static ICoreDebugSurface ResolveDebugSurface(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);

        if (coreSession.TryGetCapability<ICoreDebugSurface>(out var debugSurface))
            return debugSurface;

        return new NesConsoleDebugSurfaceAdapter(GetRequiredLegacyNesConsole(coreSession, nameof(ICoreDebugSurface)));
    }

    public static ITimeTravelService ResolveTimeTravelService(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);

        if (coreSession.TryGetCapability<ITimeTravelService>(out var timeTravelService))
            return timeTravelService;

        return new NesConsoleTimeTravelServiceAdapter(GetRequiredLegacyNesConsole(coreSession, nameof(ITimeTravelService)));
    }

    public static ICoreInputStateWriter ResolveInputStateWriter(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);

        if (coreSession.TryGetCapability<ICoreInputStateWriter>(out var inputStateWriter))
            return inputStateWriter;

        return new NesConsoleInputStateWriterAdapter(GetRequiredLegacyNesConsole(coreSession, nameof(ICoreInputStateWriter)));
    }

    public static INesRenderStateProvider ResolveNesRenderStateProvider(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);

        if (coreSession.TryGetCapability<INesRenderStateProvider>(out var renderStateProvider))
            return renderStateProvider;

        return new NesConsoleRenderStateProviderAdapter(GetRequiredLegacyNesConsole(coreSession, nameof(INesRenderStateProvider)));
    }

    private static NesConsole GetRequiredLegacyNesConsole(
        IEmulatorCoreSession coreSession,
        string capabilityName)
    {
        if (coreSession.TryGetLegacySessionObject<NesConsole>(LegacySessionAdapterIds.NesConsole, out var legacyConsole))
            return legacyConsole;

        throw new InvalidOperationException(
            $"Core '{coreSession.RuntimeInfo.CoreId}' does not expose required capability '{capabilityName}' " +
            $"and does not provide legacy adapter '{LegacySessionAdapterIds.NesConsole}' fallback.");
    }
}
