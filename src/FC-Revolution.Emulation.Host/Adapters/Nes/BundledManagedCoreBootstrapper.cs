using FCRevolution.Core.Nes.Managed;

namespace FCRevolution.Emulation.Host;

public static class BundledManagedCoreBootstrapper
{
    public const string DefaultCoreId = "fc.nes.managed";

    private static readonly Lock BootstrapGate = new();

    public static void EnsureBundledCorePackages(string? resourceRootPath)
    {
        var normalizedResourceRoot = FCRevolution.Storage.AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);

        lock (BootstrapGate)
        {
            var packageService = new ManagedCorePackageService();
            var nesModule = new NesManagedCoreModule();
            packageService.EnsureLooseManagedModuleInstalled(
                nesModule.Manifest,
                typeof(NesManagedCoreModule).Assembly.Location,
                typeof(NesManagedCoreModule).FullName ?? nameof(NesManagedCoreModule),
                normalizedResourceRoot,
                isBundled: true);
        }
    }
}
