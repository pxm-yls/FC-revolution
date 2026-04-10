using FCRevolution.Core.Nes.Managed;
using FCRevolution.Emulation.Host;

namespace FC_Revolution.UI.Tests;

internal static class NesManagedCoreTestBootstrap
{
    internal static InstalledManagedCorePackage EnsureInstalled(string? resourceRootPath, bool isBundled = true)
    {
        var module = new NesManagedCoreModule();
        return new ManagedCorePackageService().EnsureLooseManagedModuleInstalled(
            module.Manifest,
            typeof(NesManagedCoreModule).Assembly.Location,
            typeof(NesManagedCoreModule).FullName ?? nameof(NesManagedCoreModule),
            resourceRootPath,
            isBundled);
    }
}
