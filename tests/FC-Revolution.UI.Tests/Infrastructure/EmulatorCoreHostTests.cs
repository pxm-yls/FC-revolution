using FCRevolution.Core.Nes.Managed;
using FCRevolution.Core.Sample.Managed;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Storage;
using System.Reflection;

namespace FC_Revolution.UI.Tests;

public sealed class EmulatorCoreHostTests
{
    [Fact]
    public void EmulatorCoreHost_AllowsEmptyCatalog_AndTryCreateSessionReturnsFalse()
    {
        var host = new EmulatorCoreHost([]);

        Assert.False(host.HasInstalledCores);
        Assert.Empty(host.GetInstalledCoreManifests());
        Assert.Null(host.DefaultCoreId);
        Assert.False(host.TryCreateSession(new CoreSessionLaunchRequest(), out var session));
        Assert.Null(session);
        Assert.Throws<InvalidOperationException>(() => host.CreateSession(new CoreSessionLaunchRequest()));
    }

    [Fact]
    public void CreateSession_WhenCoreIdSpecified_UsesRequestedModule()
    {
        var nesModule = new FakeManagedCoreModule(
            new CoreManifest(NesManagedCoreModule.CoreId, "NES Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var host = new EmulatorCoreHost([nesModule, gbModule]);

        using var session = host.CreateSession(new CoreSessionLaunchRequest("fc.gb.managed"));

        Assert.Equal("fc.gb.managed", session.RuntimeInfo.CoreId);
        Assert.Equal(0, nesModule.Factory.CreateSessionCallCount);
        Assert.Equal(1, gbModule.Factory.CreateSessionCallCount);
    }

    [Fact]
    public void CreateSession_WhenCoreIdMissingOrUnknown_FallsBackToFirstAvailableModule()
    {
        var nesModule = new FakeManagedCoreModule(
            new CoreManifest(NesManagedCoreModule.CoreId, "NES Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var host = new EmulatorCoreHost([gbModule, nesModule]);

        using var sessionWithoutCoreId = host.CreateSession(new CoreSessionLaunchRequest());
        using var sessionWithUnknownCoreId = host.CreateSession(new CoreSessionLaunchRequest("unknown.core"));

        Assert.Equal("fc.gb.managed", sessionWithoutCoreId.RuntimeInfo.CoreId);
        Assert.Equal("fc.gb.managed", sessionWithUnknownCoreId.RuntimeInfo.CoreId);
        Assert.Equal(0, nesModule.Factory.CreateSessionCallCount);
        Assert.Equal(2, gbModule.Factory.CreateSessionCallCount);
    }

    [Fact]
    public void CreateSession_WhenDefaultCoreIdInjected_FallsBackToInjectedDefaultModule()
    {
        var nesModule = new FakeManagedCoreModule(
            new CoreManifest(NesManagedCoreModule.CoreId, "NES Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var host = new EmulatorCoreHost([nesModule, gbModule], defaultCoreId: "fc.gb.managed");

        using var sessionWithoutCoreId = host.CreateSession(new CoreSessionLaunchRequest());
        using var sessionWithUnknownCoreId = host.CreateSession(new CoreSessionLaunchRequest("unknown.core"));

        Assert.Equal("fc.gb.managed", sessionWithoutCoreId.RuntimeInfo.CoreId);
        Assert.Equal("fc.gb.managed", sessionWithUnknownCoreId.RuntimeInfo.CoreId);
        Assert.Equal(0, nesModule.Factory.CreateSessionCallCount);
        Assert.Equal(2, gbModule.Factory.CreateSessionCallCount);
    }

    [Fact]
    public void GetInstalledCoreManifests_ReturnsDisplayNameSorted()
    {
        var zetaModule = new FakeManagedCoreModule(
            new CoreManifest("core.zeta", "Zeta", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var alphaModule = new FakeManagedCoreModule(
            new CoreManifest("core.alpha", "Alpha", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var betaModule = new FakeManagedCoreModule(
            new CoreManifest("core.beta", "beta", "md", "1.0.0", CoreBinaryKinds.ManagedDotNet));
        var host = new EmulatorCoreHost([zetaModule, betaModule, alphaModule]);

        var manifests = host.GetInstalledCoreManifests();

        Assert.Equal(3, manifests.Count);
        Assert.Equal(["Alpha", "beta", "Zeta"], manifests.Select(manifest => manifest.DisplayName).ToArray());
    }

    [Fact]
    public void DefaultEmulatorCoreHost_LoadsExplicitlyInstalledBundledNesModule()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-default-emulator-core-host-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            NesManagedCoreTestBootstrap.EnsureInstalled(tempRoot);

            var host = DefaultEmulatorCoreHost.Create(
                defaultCoreId: null,
                options: new ManagedCoreRuntimeOptions(
                    ResourceRootPath: tempRoot));
            var manifests = host.GetInstalledCoreManifests();

            Assert.Contains(manifests, manifest => manifest.CoreId == NesManagedCoreModule.CoreId);
            Assert.Equal(NesManagedCoreModule.CoreId, host.DefaultCoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DefaultEmulatorCoreHost_CanSkipBundledBootstrap_AndExposeEmptyCatalog()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-default-host-empty-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);

            var host = DefaultEmulatorCoreHost.Create(
                defaultCoreId: null,
                options: new ManagedCoreRuntimeOptions(
                    ResourceRootPath: tempRoot));

            Assert.False(host.HasInstalledCores);
            Assert.Empty(host.GetInstalledCoreManifests());
            Assert.False(host.TryCreateSession(new CoreSessionLaunchRequest(), out _));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ExplicitBundledNesInstall_ReinstallsBundle_WhenResourceRootWasCleared()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-bundled-core-reinstall-{Guid.NewGuid():N}");
        var packageService = new ManagedCorePackageService();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);

            NesManagedCoreTestBootstrap.EnsureInstalled(tempRoot);
            Assert.Contains(
                packageService.GetInstalledPackages(tempRoot),
                package => package.Manifest.CoreId == NesManagedCoreModule.CoreId && package.IsBundled);

            Directory.Delete(tempRoot, recursive: true);
            Directory.CreateDirectory(tempRoot);

            NesManagedCoreTestBootstrap.EnsureInstalled(tempRoot);

            Assert.Contains(
                packageService.GetInstalledPackages(tempRoot),
                package => package.Manifest.CoreId == NesManagedCoreModule.CoreId && package.IsBundled);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DefaultNesCore_InputSchema_ContainsXYAndReservedActionsForBothPorts()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-default-nes-input-schema-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            NesManagedCoreTestBootstrap.EnsureInstalled(tempRoot);

            var host = DefaultEmulatorCoreHost.Create(
                NesManagedCoreModule.CoreId,
                new ManagedCoreRuntimeOptions(
                    ResourceRootPath: tempRoot));
            using var session = host.CreateSession(new CoreSessionLaunchRequest(NesManagedCoreModule.CoreId));

            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "x");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "y");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "l1");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "r1");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "l2");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "r2");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "l3");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p1" && action.ActionId == "r3");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "x");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "y");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "l1");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "r1");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "l2");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "r2");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "l3");
            Assert.Contains(session.InputSchema.Actions, action => action.PortId == "p2" && action.ActionId == "r3");
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DefaultManagedCoreModuleCatalog_IncludesExplicitAdditionalModulesOnly()
    {
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));

        var modules = DefaultManagedCoreModuleCatalog.CreateModules([gbModule]);

        Assert.DoesNotContain(modules, module => module.Manifest.CoreId == NesManagedCoreModule.CoreId);
        Assert.Contains(modules, module => module.Manifest.CoreId == "fc.gb.managed");
    }

    [Fact]
    public void DefaultManagedCoreModuleCatalog_RegisterAdditionalModule_ExtendsDefaultHost()
    {
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));

        try
        {
            DefaultManagedCoreModuleCatalog.RegisterAdditionalModule(gbModule);

            var modules = DefaultManagedCoreModuleCatalog.CreateModules();
            var host = DefaultEmulatorCoreHost.Create("fc.gb.managed");
            using var session = host.CreateSession(new CoreSessionLaunchRequest());

            Assert.Contains(modules, module => module.Manifest.CoreId == "fc.gb.managed");
            Assert.Equal("fc.gb.managed", session.RuntimeInfo.CoreId);
            Assert.Equal(1, gbModule.Factory.CreateSessionCallCount);
        }
        finally
        {
            DefaultManagedCoreModuleCatalog.UnregisterAdditionalModule("fc.gb.managed");
        }
    }

    [Fact]
    public void DefaultManagedCoreModuleCatalog_RegisterAdditionalModuleSource_ExtendsDefaultHost()
    {
        var source = new FixedManagedCoreModuleRegistrationSource(
            "test.source",
            [new DiscoverableManagedCoreModule()]);

        try
        {
            DefaultManagedCoreModuleCatalog.RegisterAdditionalModuleSource(source);

            var host = DefaultEmulatorCoreHost.Create(DiscoverableManagedCoreModule.CoreId);
            using var session = host.CreateSession(new CoreSessionLaunchRequest());

            Assert.Equal(DiscoverableManagedCoreModule.CoreId, session.RuntimeInfo.CoreId);
            Assert.Contains(host.GetInstalledCoreManifests(), manifest => manifest.CoreId == DiscoverableManagedCoreModule.CoreId);
        }
        finally
        {
            DefaultManagedCoreModuleCatalog.UnregisterAdditionalModuleSource("test.source");
        }
    }

    [Fact]
    public void DirectoryManagedCoreModuleRegistrationSource_LoadModules_DiscoversModulesFromAssemblyPath()
    {
        var assembly = typeof(DiscoverableManagedCoreModule).Assembly;
        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(assemblyDirectory));

        var source = new DirectoryManagedCoreModuleRegistrationSource(
            "directory.source",
            () => [assemblyDirectory!],
            Path.GetFileName(assembly.Location),
            SearchOption.TopDirectoryOnly);

        var modules = source.LoadModules();

        Assert.Contains(modules, module => module.Manifest.CoreId == DiscoverableManagedCoreModule.CoreId);
    }

    [Fact]
    public void DirectoryManagedCoreModuleRegistrationSource_LoadModules_DiscoversSampleManagedCore_AndHostCanRunFrame()
    {
        var assembly = typeof(SampleManagedCoreModule).Assembly;
        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(assemblyDirectory));

        var source = new DirectoryManagedCoreModuleRegistrationSource(
            "sample.directory.source",
            () => [assemblyDirectory!],
            Path.GetFileName(assembly.Location),
            SearchOption.TopDirectoryOnly);

        var modules = source.LoadModules();
        var sampleModule = Assert.Single(modules, module => module.Manifest.CoreId == SampleManagedCoreModule.CoreId);
        var host = new EmulatorCoreHost([new FakeManagedCoreModule(
            new CoreManifest(NesManagedCoreModule.CoreId, "NES Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet)), sampleModule], SampleManagedCoreModule.CoreId);
        var tempRomPath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(tempRomPath, "sample-core"u8.ToArray());
            using var session = host.CreateSession(new CoreSessionLaunchRequest());
            var videoPackets = new List<VideoFramePacket>();
            session.VideoFrameReady += videoPackets.Add;

            var loadResult = session.LoadMedia(new CoreMediaLoadRequest(tempRomPath));
            var stepResult = session.RunFrame();

            Assert.True(loadResult.Success);
            Assert.True(stepResult.Success);
            Assert.Equal(SampleManagedCoreModule.CoreId, session.RuntimeInfo.CoreId);
            Assert.True(session.TryGetCapability<ICoreDebugSurface>(out _));
            Assert.True(session.TryGetCapability<ITimeTravelService>(out _));
            Assert.True(session.TryGetCapability<ICoreInputStateWriter>(out _));
            Assert.True(session.TryGetCapability<ILayeredFrameProvider>(out _));
            Assert.NotEmpty(videoPackets);
            Assert.Equal(256, videoPackets[^1].Width);
            Assert.Equal(240, videoPackets[^1].Height);
        }
        finally
        {
            File.Delete(tempRomPath);
        }
    }

    [Fact]
    public void DefaultEmulatorCoreHost_Create_WithAdditionalModules_UsesInjectedDefaultCore()
    {
        var gbModule = new FakeManagedCoreModule(
            new CoreManifest("fc.gb.managed", "GB Core", "gb", "1.0.0", CoreBinaryKinds.ManagedDotNet));

        var host = DefaultEmulatorCoreHost.Create("fc.gb.managed", [gbModule]);
        using var session = host.CreateSession(new CoreSessionLaunchRequest());

        Assert.Equal("fc.gb.managed", session.RuntimeInfo.CoreId);
        Assert.Equal(1, gbModule.Factory.CreateSessionCallCount);
        Assert.Contains(host.GetInstalledCoreManifests(), manifest => manifest.CoreId == "fc.gb.managed");
    }

    [Fact]
    public void DefaultManagedCoreModuleCatalog_RegisterAdditionalModulesFromAssembly_DiscoversManagedCoreModuleTypes()
    {
        try
        {
            var modules = DefaultManagedCoreModuleCatalog.RegisterAdditionalModulesFromAssembly(typeof(DiscoverableManagedCoreModule).Assembly);
            var host = DefaultEmulatorCoreHost.Create(DiscoverableManagedCoreModule.CoreId);
            using var session = host.CreateSession(new CoreSessionLaunchRequest());

            Assert.Contains(modules, module => module.Manifest.CoreId == DiscoverableManagedCoreModule.CoreId);
            Assert.Equal(DiscoverableManagedCoreModule.CoreId, session.RuntimeInfo.CoreId);
        }
        finally
        {
            DefaultManagedCoreModuleCatalog.UnregisterAdditionalModule(DiscoverableManagedCoreModule.CoreId);
        }
    }

    private sealed class FakeManagedCoreModule : IManagedCoreModule
    {
        public FakeManagedCoreModule(CoreManifest manifest)
        {
            Manifest = manifest;
            Factory = new FakeEmulatorCoreFactory(manifest);
        }

        public CoreManifest Manifest { get; }

        public FakeEmulatorCoreFactory Factory { get; }

        public IEmulatorCoreFactory CreateFactory() => Factory;
    }

    private sealed class FakeEmulatorCoreFactory : IEmulatorCoreFactory
    {
        private readonly CoreManifest _manifest;

        public FakeEmulatorCoreFactory(CoreManifest manifest)
        {
            _manifest = manifest;
        }

        public int CreateSessionCallCount { get; private set; }

        public IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options)
        {
            CreateSessionCallCount++;
            return new FakeEmulatorCoreSession(_manifest);
        }
    }

    private sealed class FakeEmulatorCoreSession : IEmulatorCoreSession
    {
        public FakeEmulatorCoreSession(CoreManifest manifest)
        {
            RuntimeInfo = new CoreRuntimeInfo(
                manifest.CoreId,
                manifest.DisplayName,
                manifest.SystemId,
                manifest.Version,
                manifest.BinaryKind);
        }

        public event Action<VideoFramePacket>? VideoFrameReady
        {
            add { }
            remove { }
        }

        public event Action<AudioPacket>? AudioReady
        {
            add { }
            remove { }
        }

        public CoreRuntimeInfo RuntimeInfo { get; }

        public CoreCapabilitySet Capabilities { get; } = CoreCapabilitySet.From();

        public IInputSchema InputSchema { get; } = new EmptyInputSchema();

        public CoreLoadResult LoadMedia(CoreMediaLoadRequest request) => CoreLoadResult.Ok();

        public void Reset()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public CoreStepResult RunFrame() => CoreStepResult.Ok();

        public CoreStepResult StepInstruction() => CoreStepResult.Ok();

        public CoreStateBlob CaptureState(bool includeThumbnail = false) =>
            new()
            {
                Format = "fake/state",
                Data = []
            };

        public void RestoreState(CoreStateBlob state)
        {
        }

        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
            capability = null!;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }

    public sealed class DiscoverableManagedCoreModule : IManagedCoreModule
    {
        public const string CoreId = "fc.discoverable.managed";

        private static readonly CoreManifest DiscoverableManifest =
            new(CoreId, "Discoverable Core", "test", "1.0.0", CoreBinaryKinds.ManagedDotNet);

        public CoreManifest Manifest => DiscoverableManifest;

        public IEmulatorCoreFactory CreateFactory() => new FakeEmulatorCoreFactory(DiscoverableManifest);
    }

    private sealed class FixedManagedCoreModuleRegistrationSource : IEmulatorCoreModuleRegistrationSource
    {
        private readonly IReadOnlyList<IEmulatorCoreModule> _modules;

        public FixedManagedCoreModuleRegistrationSource(string sourceId, IReadOnlyList<IEmulatorCoreModule> modules)
        {
            SourceId = sourceId;
            _modules = modules;
        }

        public string SourceId { get; }

        public IReadOnlyList<IEmulatorCoreModule> LoadModules() => _modules;
    }
}
