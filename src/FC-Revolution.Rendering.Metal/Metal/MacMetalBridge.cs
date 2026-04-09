using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Rendering.Metal;

internal static partial class MacMetalBridge
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePresenterDiagnostics
    {
        public uint RequestedUpscaleMode;
        public uint EffectiveUpscaleMode;
        public uint FallbackReason;
        public uint InternalWidth;
        public uint InternalHeight;
        public uint OutputWidth;
        public uint OutputHeight;
        public uint DrawableWidth;
        public uint DrawableHeight;
        public double TargetWidthPoints;
        public double TargetHeightPoints;
        public double DisplayScale;
        public double HostWidthPoints;
        public double HostHeightPoints;
        public double LayerWidthPoints;
        public double LayerHeightPoints;
        public uint TemporalResetPending;
        public uint TemporalResetApplied;
        public uint TemporalResetCount;
        public uint TemporalResetReason;
    }

    private const string LibraryName = "FCRMetalBridge";
    private const string DylibName = "libFCRMetalBridge.dylib";
    private static readonly string? LibraryPath = ResolveLibraryPath();

    static MacMetalBridge()
    {
        NativeLibrary.SetDllImportResolver(typeof(MacMetalBridge).Assembly, ResolveLibraryImport);
    }

    public static bool IsAvailable => OperatingSystem.IsMacOS() && LibraryPath != null;

    public static string? UnavailableReason => OperatingSystem.IsMacOS()
        ? LibraryPath == null
            ? "未找到 libFCRMetalBridge.dylib"
            : null
        : "当前平台不是 macOS";

    private static IntPtr ResolveLibraryImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
            return IntPtr.Zero;

        if (LibraryPath != null && NativeLibrary.TryLoad(LibraryPath, out var handle))
            return handle;

        return IntPtr.Zero;
    }

    private static string? ResolveLibraryPath()
    {
        if (!OperatingSystem.IsMacOS())
            return null;

        foreach (var candidate in EnumerateCandidatePaths())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        var baseDirectory = AppContext.BaseDirectory;

        yield return Path.Combine(baseDirectory, DylibName);
        yield return Path.Combine(baseDirectory, "FCRMetalBridge.dylib");

        var runtimeIdentifier = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "osx-arm64",
            Architecture.X64 => "osx-x64",
            _ => null
        };

        if (runtimeIdentifier == null)
            yield break;

        yield return Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native", DylibName);
        yield return Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native", "FCRMetalBridge.dylib");
    }

    [LibraryImport(LibraryName, EntryPoint = "FCR_CreateMetalPresenter")]
    internal static partial IntPtr CreateMetalPresenter(IntPtr parentView, uint frameWidth, uint frameHeight, MacUpscaleMode upscaleMode);

    [LibraryImport(LibraryName, EntryPoint = "FCR_GetMetalPresenterViewHandle")]
    internal static partial IntPtr GetMetalPresenterViewHandle(IntPtr presenter);

    [LibraryImport(LibraryName, EntryPoint = "FCR_SetMetalPresenterUpscaleMode")]
    internal static partial void SetMetalPresenterUpscaleMode(IntPtr presenter, MacUpscaleMode upscaleMode);

    [LibraryImport(LibraryName, EntryPoint = "FCR_SetMetalPresenterUpscaleOutputResolution")]
    internal static partial void SetMetalPresenterUpscaleOutputResolution(IntPtr presenter, MacUpscaleOutputResolution outputResolution);

    [LibraryImport(LibraryName, EntryPoint = "FCR_SetMetalPresenterDisplaySize")]
    internal static partial void SetMetalPresenterDisplaySize(IntPtr presenter, double widthPoints, double heightPoints);

    [LibraryImport(LibraryName, EntryPoint = "FCR_SetMetalPresenterCornerRadius")]
    internal static partial void SetMetalPresenterCornerRadius(IntPtr presenter, double radiusPoints);

    [LibraryImport(LibraryName, EntryPoint = "FCR_RequestMetalPresenterTemporalHistoryReset")]
    internal static partial void RequestMetalPresenterTemporalHistoryReset(IntPtr presenter, MacMetalTemporalResetReason resetReason);

    [LibraryImport(LibraryName, EntryPoint = "FCR_GetMetalPresenterDiagnostics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool GetMetalPresenterDiagnosticsNative(IntPtr presenter, out NativePresenterDiagnostics diagnostics);

    [LibraryImport(LibraryName, EntryPoint = "FCR_PresentFrame")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool PresentFrame(IntPtr presenter, IntPtr pixels, uint pixelCount);

    [LibraryImport(LibraryName, EntryPoint = "FCR_PresentLayeredFrame")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool PresentLayeredFrame(
        IntPtr presenter,
        IntPtr chrAtlas,
        uint chrAtlasLength,
        IntPtr palette,
        uint paletteLength,
        IntPtr backgroundTiles,
        uint backgroundTileCount,
        IntPtr sprites,
        uint spriteCount,
        byte showBackground,
        byte showSprites,
        byte showBackgroundLeft8,
        byte showSpritesLeft8);

    [LibraryImport(LibraryName, EntryPoint = "FCR_RenderLayeredFrameOffscreen")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool RenderLayeredFrameOffscreen(
        IntPtr chrAtlas,
        uint chrAtlasLength,
        IntPtr palette,
        uint paletteLength,
        IntPtr backgroundTiles,
        uint backgroundTileCount,
        IntPtr sprites,
        uint spriteCount,
        byte showBackground,
        byte showSprites,
        byte showBackgroundLeft8,
        byte showSpritesLeft8,
        uint frameWidth,
        uint frameHeight,
        IntPtr outputPixels,
        uint outputPixelCount);

    [LibraryImport(LibraryName, EntryPoint = "FCR_RenderLayeredFrameOffscreenEx")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool RenderLayeredFrameOffscreenEx(
        IntPtr chrAtlas,
        uint chrAtlasLength,
        IntPtr palette,
        uint paletteLength,
        IntPtr backgroundTiles,
        uint backgroundTileCount,
        IntPtr sprites,
        uint spriteCount,
        byte showBackground,
        byte showSprites,
        byte showBackgroundLeft8,
        byte showSpritesLeft8,
        uint frameWidth,
        uint frameHeight,
        MacUpscaleMode upscaleMode,
        uint outputWidth,
        uint outputHeight,
        IntPtr outputPixels,
        uint outputPixelCount);

    [LibraryImport(LibraryName, EntryPoint = "FCR_RenderLayeredFrameOffscreenExWithMotionTexture")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool RenderLayeredFrameOffscreenExWithMotionTexture(
        IntPtr chrAtlas,
        uint chrAtlasLength,
        IntPtr palette,
        uint paletteLength,
        IntPtr backgroundTiles,
        uint backgroundTileCount,
        IntPtr sprites,
        uint spriteCount,
        byte showBackground,
        byte showSprites,
        byte showBackgroundLeft8,
        byte showSpritesLeft8,
        uint frameWidth,
        uint frameHeight,
        MacUpscaleMode upscaleMode,
        uint outputWidth,
        uint outputHeight,
        IntPtr motionTextureBytes,
        uint motionTextureByteLength,
        uint motionTextureWidth,
        uint motionTextureHeight,
        IntPtr outputPixels,
        uint outputPixelCount);

    [LibraryImport(LibraryName, EntryPoint = "FCR_DestroyMetalPresenter")]
    internal static partial void DestroyMetalPresenter(IntPtr presenter);

    internal static bool TryGetPresenterDiagnostics(IntPtr presenter, out MacMetalPresenterDiagnostics diagnostics)
    {
        diagnostics = MacMetalPresenterDiagnostics.Empty;
        if (presenter == IntPtr.Zero)
            return false;

        if (!GetMetalPresenterDiagnosticsNative(presenter, out NativePresenterDiagnostics native))
            return false;

        diagnostics = new MacMetalPresenterDiagnostics(
            RequestedUpscaleMode: (MacUpscaleMode)native.RequestedUpscaleMode,
            EffectiveUpscaleMode: (MacUpscaleMode)native.EffectiveUpscaleMode,
            FallbackReason: (MacMetalFallbackReason)native.FallbackReason,
            InternalWidth: native.InternalWidth,
            InternalHeight: native.InternalHeight,
            OutputWidth: native.OutputWidth,
            OutputHeight: native.OutputHeight,
            DrawableWidth: native.DrawableWidth,
            DrawableHeight: native.DrawableHeight,
            TargetWidthPoints: native.TargetWidthPoints,
            TargetHeightPoints: native.TargetHeightPoints,
            DisplayScale: native.DisplayScale,
            HostWidthPoints: native.HostWidthPoints,
            HostHeightPoints: native.HostHeightPoints,
            LayerWidthPoints: native.LayerWidthPoints,
            LayerHeightPoints: native.LayerHeightPoints,
            TemporalResetPending: native.TemporalResetPending != 0,
            TemporalResetApplied: native.TemporalResetApplied != 0,
            TemporalResetCount: native.TemporalResetCount,
            TemporalResetReason: (MacMetalTemporalResetReason)native.TemporalResetReason);
        return true;
    }
}
