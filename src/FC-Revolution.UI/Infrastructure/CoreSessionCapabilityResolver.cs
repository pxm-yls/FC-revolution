using System;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class CoreSessionCapabilityResolver
{
    public static ICoreDebugSurface ResolveDebugSurface(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);
        return RequireCapability<ICoreDebugSurface>(coreSession);
    }

    public static ITimeTravelService ResolveTimeTravelService(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);
        return RequireCapability<ITimeTravelService>(coreSession);
    }

    public static ICoreInputStateWriter ResolveInputStateWriter(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);
        return RequireCapability<ICoreInputStateWriter>(coreSession);
    }

    public static ILayeredFrameProvider ResolveLayeredFrameProvider(IEmulatorCoreSession coreSession)
    {
        ArgumentNullException.ThrowIfNull(coreSession);
        return RequireCapability<ILayeredFrameProvider>(coreSession);
    }

    private static TCapability RequireCapability<TCapability>(IEmulatorCoreSession coreSession)
        where TCapability : class
    {
        if (coreSession.TryGetCapability<TCapability>(out var capability))
            return capability;

        throw new InvalidOperationException(
            $"Core '{coreSession.RuntimeInfo.CoreId}' does not expose required capability '{typeof(TCapability).Name}'.");
    }
}
