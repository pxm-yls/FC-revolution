using System.Runtime.InteropServices;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.CoreLoader.Native;

public sealed class NativeCoreLibraryHandle : IDisposable
{
    private readonly IntPtr _libraryHandle;
    private readonly CreateSessionDelegate _createSession;
    private readonly DestroySessionDelegate _destroySession;
    private readonly LoadMediaDelegate _loadMedia;
    private readonly VoidSessionDelegate _reset;
    private readonly VoidSessionDelegate _pause;
    private readonly VoidSessionDelegate _resume;
    private readonly IntSessionDelegate _runFrame;
    private readonly IntSessionDelegate _stepInstruction;
    private readonly BufferCopyDelegate _captureState;
    private readonly RestoreStateDelegate _restoreState;
    private bool _disposed;

    internal NativeCoreLibraryHandle(
        string libraryPath,
        IntPtr libraryHandle,
        CoreManifest manifest,
        NativeCoreModuleLoader.NativeCoreApi api)
    {
        LibraryPath = libraryPath;
        Manifest = manifest;
        _libraryHandle = libraryHandle;
        _createSession = GetRequiredDelegate<CreateSessionDelegate>(api.CreateSession, "create_session");
        _destroySession = GetRequiredDelegate<DestroySessionDelegate>(api.DestroySession, "destroy_session");
        _loadMedia = GetRequiredDelegate<LoadMediaDelegate>(api.LoadMedia, "load_media");
        _reset = GetRequiredDelegate<VoidSessionDelegate>(api.Reset, "reset");
        _pause = GetRequiredDelegate<VoidSessionDelegate>(api.Pause, "pause");
        _resume = GetRequiredDelegate<VoidSessionDelegate>(api.Resume, "resume");
        _runFrame = GetRequiredDelegate<IntSessionDelegate>(api.RunFrame, "run_frame");
        _stepInstruction = GetRequiredDelegate<IntSessionDelegate>(api.StepInstruction, "step_instruction");
        _captureState = GetRequiredDelegate<BufferCopyDelegate>(api.CaptureState, "capture_state");
        _restoreState = GetRequiredDelegate<RestoreStateDelegate>(api.RestoreState, "restore_state");
    }

    public string LibraryPath { get; }

    public CoreManifest Manifest { get; }

    public IntPtr CreateSession() => _createSession(IntPtr.Zero);

    public void DestroySession(IntPtr session)
    {
        if (session != IntPtr.Zero)
            _destroySession(session);
    }

    public int LoadMedia(IntPtr session, byte[] mediaBytes, string? fileName)
    {
        ArgumentNullException.ThrowIfNull(mediaBytes);

        using var pinnedMedia = new PinnedBuffer(mediaBytes);
        var fileNamePointer = Marshal.StringToCoTaskMemUTF8(fileName ?? string.Empty);
        try
        {
            return _loadMedia(session, pinnedMedia.Pointer, mediaBytes.Length, fileNamePointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fileNamePointer);
        }
    }

    public void Reset(IntPtr session) => _reset(session);

    public void Pause(IntPtr session) => _pause(session);

    public void Resume(IntPtr session) => _resume(session);

    public int RunFrame(IntPtr session) => _runFrame(session);

    public int StepInstruction(IntPtr session) => _stepInstruction(session);

    public byte[] CaptureState(IntPtr session)
    {
        var requiredSize = _captureState(session, IntPtr.Zero, 0);
        if (requiredSize <= 0)
            return [];

        var buffer = new byte[requiredSize];
        using var pinnedBuffer = new PinnedBuffer(buffer);
        var actualSize = _captureState(session, pinnedBuffer.Pointer, buffer.Length);
        if (actualSize <= 0)
            return [];

        if (actualSize == buffer.Length)
            return buffer;

        return buffer[..actualSize];
    }

    public void RestoreState(IntPtr session, byte[] stateBytes)
    {
        ArgumentNullException.ThrowIfNull(stateBytes);

        using var pinnedState = new PinnedBuffer(stateBytes);
        var result = _restoreState(session, pinnedState.Pointer, stateBytes.Length);
        if (result <= 0)
            throw new NativeCoreLoadException("Native core failed to restore state.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        NativeLibrary.Free(_libraryHandle);
        _disposed = true;
    }

    private static TDelegate GetRequiredDelegate<TDelegate>(IntPtr pointer, string memberName)
        where TDelegate : Delegate
    {
        if (pointer == IntPtr.Zero)
            throw new NativeCoreLoadException($"Native core API is missing '{memberName}'.");

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(pointer);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateSessionDelegate(IntPtr hostApi);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DestroySessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadMediaDelegate(IntPtr session, IntPtr data, int length, IntPtr fileName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VoidSessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IntSessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BufferCopyDelegate(IntPtr session, IntPtr destination, int destinationLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RestoreStateDelegate(IntPtr session, IntPtr data, int length);

    private sealed class PinnedBuffer : IDisposable
    {
        private readonly GCHandle _handle;

        public PinnedBuffer(byte[] buffer)
        {
            _handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Pointer = _handle.AddrOfPinnedObject();
        }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
