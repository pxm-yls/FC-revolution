using System.Runtime.InteropServices;
using System.Text.Json;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.CoreLoader.Native;

public sealed record NativeCoreInspectionResult(
    bool Success,
    CoreManifest? Manifest,
    string? FailureReason);

public static class NativeCoreModuleLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static NativeCoreInspectionResult Inspect(string libraryPath)
    {
        try
        {
            using var handle = Load(libraryPath);
            return new NativeCoreInspectionResult(true, handle.Manifest, null);
        }
        catch (Exception ex)
        {
            return new NativeCoreInspectionResult(false, null, ex.Message);
        }
    }

    public static NativeCoreLibraryHandle Load(string libraryPath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
            throw new NativeCoreLoadException("Native core entry path is required.");

        var fullPath = Path.GetFullPath(libraryPath);
        if (!File.Exists(fullPath))
            throw new NativeCoreLoadException($"Native core library not found: {fullPath}");

        IntPtr libraryHandle;
        try
        {
            libraryHandle = NativeLibrary.Load(fullPath);
        }
        catch (Exception ex)
        {
            throw new NativeCoreLoadException($"Failed to load native core library '{fullPath}': {ex.Message}");
        }

        try
        {
            if (!NativeLibrary.TryGetExport(libraryHandle, NativeCoreAbiConstants.GetCoreApiSymbol, out var getCoreApiPointer) ||
                getCoreApiPointer == IntPtr.Zero)
            {
                throw new NativeCoreLoadException(
                    $"Native core library '{fullPath}' does not export {NativeCoreAbiConstants.GetCoreApiSymbol}.");
            }

            var getCoreApi = Marshal.GetDelegateForFunctionPointer<GetCoreApiDelegate>(getCoreApiPointer);
            var apiPointer = getCoreApi(NativeCoreAbiConstants.CurrentAbiVersion);
            if (apiPointer == IntPtr.Zero)
                throw new NativeCoreLoadException("Native core rejected the current host ABI version.");

            var api = Marshal.PtrToStructure<NativeCoreApi>(apiPointer);
            if (api.AbiVersion != NativeCoreAbiConstants.CurrentAbiVersion)
            {
                throw new NativeCoreLoadException(
                    $"Native core ABI mismatch: expected {NativeCoreAbiConstants.CurrentAbiVersion}, actual {api.AbiVersion}.");
            }

            if (api.StructSize < Marshal.SizeOf<NativeCoreApi>())
                throw new NativeCoreLoadException("Native core API struct is smaller than the host expects.");

            var getManifestJson = RequireDelegate<GetUtf8StringDelegate>(api.GetManifestJson, "get_manifest_json");
            var manifestJsonPointer = getManifestJson();
            var manifestJson = Marshal.PtrToStringUTF8(manifestJsonPointer);
            if (string.IsNullOrWhiteSpace(manifestJson))
                throw new NativeCoreLoadException("Native core did not provide manifest JSON.");

            var manifest = JsonSerializer.Deserialize<CoreManifest>(manifestJson, JsonOptions)
                ?? throw new NativeCoreLoadException("Native core manifest JSON could not be parsed.");

            if (!string.Equals(manifest.BinaryKind, CoreBinaryKinds.NativeCabi, StringComparison.OrdinalIgnoreCase))
            {
                throw new NativeCoreLoadException(
                    $"Native core manifest binaryKind must be '{CoreBinaryKinds.NativeCabi}', actual '{manifest.BinaryKind}'.");
            }

            var exportedCoreId = string.Empty;
            if (api.GetCoreId != IntPtr.Zero)
            {
                var getCoreId = Marshal.GetDelegateForFunctionPointer<GetUtf8StringDelegate>(api.GetCoreId);
                exportedCoreId = Marshal.PtrToStringUTF8(getCoreId()) ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(exportedCoreId) &&
                !string.Equals(exportedCoreId, manifest.CoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new NativeCoreLoadException(
                    $"Native core manifest id '{manifest.CoreId}' does not match exported core id '{exportedCoreId}'.");
            }

            return new NativeCoreLibraryHandle(fullPath, libraryHandle, manifest, api);
        }
        catch
        {
            NativeLibrary.Free(libraryHandle);
            throw;
        }
    }

    private static TDelegate RequireDelegate<TDelegate>(IntPtr pointer, string memberName)
        where TDelegate : Delegate
    {
        if (pointer == IntPtr.Zero)
            throw new NativeCoreLoadException($"Native core API is missing '{memberName}'.");

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(pointer);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetCoreApiDelegate(uint hostAbiVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr GetUtf8StringDelegate();

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCoreApi
    {
        public uint AbiVersion;
        public uint StructSize;
        public IntPtr GetCoreId;
        public IntPtr GetManifestJson;
        public IntPtr CreateSession;
        public IntPtr DestroySession;
        public IntPtr LoadMedia;
        public IntPtr Reset;
        public IntPtr Pause;
        public IntPtr Resume;
        public IntPtr RunFrame;
        public IntPtr StepInstruction;
        public IntPtr CopyVideoFrame;
        public IntPtr CopyAudioPacket;
        public IntPtr CaptureState;
        public IntPtr RestoreState;
    }
}
