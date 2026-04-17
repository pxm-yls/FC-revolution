using FCRevolution.CoreLoader.Native;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

internal sealed class NativeCabiInternalCoreLoader : IInternalCoreLoader
{
    public string BinaryKind => CoreBinaryKinds.NativeCabi;

    public IReadOnlyList<IEmulatorCoreModule> LoadModules(InternalCoreLoadTarget target)
    {
        var inspection = NativeCoreModuleLoader.Inspect(target.EntryPath);
        if (!inspection.Success || inspection.Manifest is null)
            return [];

        return [new NativeCabiCoreModule(inspection.Manifest, target.EntryPath)];
    }

    public IReadOnlyList<InternalDiscoveredCoreLoadTarget> DiscoverModules(InternalCoreLoadTarget target)
    {
        var inspection = NativeCoreModuleLoader.Inspect(target.EntryPath);
        if (!inspection.Success || inspection.Manifest is null)
            return [];

        return [new InternalDiscoveredCoreLoadTarget(inspection.Manifest, target)];
    }

    public InternalCoreLoadSupport GetLoadSupport(InternalCoreLoadTarget target)
    {
        var inspection = NativeCoreModuleLoader.Inspect(target.EntryPath);
        return inspection.Success
            ? InternalCoreLoadSupport.Ready()
            : InternalCoreLoadSupport.Unsupported(
                inspection.FailureReason ?? "Native core inspection failed.");
    }
}

internal sealed class NativeCabiCoreModule(CoreManifest manifest, string entryPath) : IEmulatorCoreModule
{
    public CoreManifest Manifest { get; } = manifest;

    public IEmulatorCoreFactory CreateFactory() => new NativeCabiCoreFactory(Manifest, entryPath);
}

internal sealed class NativeCabiCoreFactory(CoreManifest manifest, string entryPath) : IEmulatorCoreFactory
{
    public IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options) =>
        new NativeCabiCoreSession(manifest, entryPath);
}

internal sealed class NativeCabiCoreSession : IEmulatorCoreSession
{
    private readonly NativeCoreLibraryHandle _libraryHandle;
    private readonly IntPtr _sessionHandle;

    public NativeCabiCoreSession(CoreManifest manifest, string entryPath)
    {
        _libraryHandle = NativeCoreModuleLoader.Load(entryPath);
        _sessionHandle = _libraryHandle.CreateSession();
        if (_sessionHandle == IntPtr.Zero)
            throw new NativeCoreLoadException("Native core failed to create a session.");

        RuntimeInfo = new CoreRuntimeInfo(
            manifest.CoreId,
            manifest.DisplayName,
            manifest.SystemId,
            manifest.Version,
            manifest.BinaryKind);
        Capabilities = BuildCapabilities();
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

    public CoreCapabilitySet Capabilities { get; }

    public IInputSchema InputSchema { get; } = new EmptyInputSchema();

    public CoreLoadResult LoadMedia(CoreMediaLoadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MediaPath))
            return CoreLoadResult.Fail("Media path is required.");
        if (!File.Exists(request.MediaPath))
            return CoreLoadResult.Fail($"Media file not found: {request.MediaPath}");

        try
        {
            var mediaBytes = File.ReadAllBytes(request.MediaPath);
            var result = _libraryHandle.LoadMedia(_sessionHandle, mediaBytes, Path.GetFileName(request.MediaPath));
            return result > 0
                ? CoreLoadResult.Ok()
                : CoreLoadResult.Fail("Native core rejected the requested media.");
        }
        catch (Exception ex)
        {
            return CoreLoadResult.Fail(ex.Message);
        }
    }

    public void Reset() => _libraryHandle.Reset(_sessionHandle);

    public void Pause() => _libraryHandle.Pause(_sessionHandle);

    public void Resume() => _libraryHandle.Resume(_sessionHandle);

    public CoreStepResult RunFrame()
    {
        var result = _libraryHandle.RunFrame(_sessionHandle);
        return result > 0
            ? CoreStepResult.Ok()
            : CoreStepResult.Fail("Native core run_frame returned failure.");
    }

    public CoreStepResult StepInstruction()
    {
        var result = _libraryHandle.StepInstruction(_sessionHandle);
        return result > 0
            ? CoreStepResult.Ok()
            : CoreStepResult.Fail("Native core step_instruction returned failure.");
    }

    public CoreStateBlob CaptureState(bool includeThumbnail = false) => new()
    {
        Format = NativeCoreAbiConstants.StateFormat,
        Data = _libraryHandle.CaptureState(_sessionHandle),
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["coreId"] = RuntimeInfo.CoreId,
            ["binaryKind"] = RuntimeInfo.BinaryKind
        }
    };

    public void RestoreState(CoreStateBlob state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _libraryHandle.RestoreState(_sessionHandle, state.Data);
    }

    public bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class
    {
        capability = null!;
        return false;
    }

    public void Dispose()
    {
        _libraryHandle.DestroySession(_sessionHandle);
        _libraryHandle.Dispose();
    }

    private CoreCapabilitySet BuildCapabilities() => CoreCapabilitySet.From(
        CoreCapabilityIds.MediaLoad,
        CoreCapabilityIds.SaveState);

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}
