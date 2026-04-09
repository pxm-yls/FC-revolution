using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FC_Revolution.UI.Infrastructure;

internal static class FFmpegRuntimeBootstrap
{
    private const uint LoadWithAlteredSearchPath = 0x00000008;
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, nint> LoadedLibraryHandles = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (SyncRoot)
        {
            if (_initialized)
                return;

            var runtimePath = ResolveBundledRuntimePath()
                ?? BundledAssetExtractor.TryExtractDirectory(GetBundledRuntimeDirectory())
                ?? ResolveDevelopmentRuntimePath()
                ?? throw new DirectoryNotFoundException($"未找到 FFmpeg 运行库目录: {Path.Combine(AppContext.BaseDirectory, "runtimes", GetPlatformRuntimeFolder(), "native")}");

            ffmpeg.RootPath = runtimePath;
            ffmpeg.GetOrLoadLibrary = libraryName => GetOrLoadLibrary(runtimePath, libraryName);
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
            ffmpeg.avformat_network_init();
            _initialized = true;
        }
    }

    public static string GetBundledToolPath()
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        var platformFolder = GetPlatformRuntimeFolder();

        var bundledPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg-tools", platformFolder, fileName);
        if (File.Exists(bundledPath))
            return bundledPath;

        var extractedToolPath = BundledAssetExtractor.TryExtractFile(GetBundledToolRelativePath(fileName), executable: !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        if (File.Exists(extractedToolPath))
            return extractedToolPath;

        foreach (var baseDirectory in EnumerateSearchRoots(AppContext.BaseDirectory))
        {
            var projectLocalPath = Path.Combine(baseDirectory, "FC-Revolution.ffmpeg", "tools", platformFolder, fileName);
            if (File.Exists(projectLocalPath))
                return projectLocalPath;

            var repoLocalPath = Path.Combine(baseDirectory, "src", "FC-Revolution.ffmpeg", "tools", platformFolder, fileName);
            if (File.Exists(repoLocalPath))
                return repoLocalPath;
        }

        throw new FileNotFoundException("未找到内置 FFmpeg 工具。", bundledPath);
    }

    private static string? ResolveBundledRuntimePath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "runtimes", GetPlatformRuntimeFolder(), "native");
        return Directory.Exists(bundledPath) ? bundledPath : null;
    }

    private static string? ResolveDevelopmentRuntimePath()
    {
        var platformFolder = GetPlatformRuntimeFolder();
        foreach (var baseDirectory in EnumerateSearchRoots(AppContext.BaseDirectory))
        {
            var projectLocalPath = Path.Combine(baseDirectory, "FC-Revolution.ffmpeg", "runtimes", platformFolder, "native");
            if (Directory.Exists(projectLocalPath))
                return projectLocalPath;

            var repoLocalPath = Path.Combine(baseDirectory, "src", "FC-Revolution.ffmpeg", "runtimes", platformFolder, "native");
            if (Directory.Exists(repoLocalPath))
                return repoLocalPath;
        }

        return null;
    }

    private static string GetBundledRuntimeDirectory()
        => $"runtimes/{GetPlatformRuntimeFolder()}/native";

    private static string GetBundledToolRelativePath(string fileName)
        => $"tools/{GetPlatformRuntimeFolder()}/{fileName}";

    private static nint GetOrLoadLibrary(string rootPath, string libraryName)
    {
        var cacheKey = $"{rootPath}|{libraryName}";
        return LoadedLibraryHandles.GetOrAdd(cacheKey, _ => LoadLibrary(rootPath, libraryName));
    }

    private static nint LoadLibrary(string rootPath, string libraryName)
    {
        foreach (var candidatePath in EnumerateLibraryCandidates(rootPath, libraryName))
        {
            if (!File.Exists(candidatePath))
                continue;

            try
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? LoadWindowsLibrary(candidatePath)
                    : NativeLibrary.Load(candidatePath);
            }
            catch
            {
            }
        }

        throw new DllNotFoundException($"Unable to load DLL '{libraryName} under {rootPath}': The specified module could not be found.");
    }

    private static nint LoadWindowsLibrary(string candidatePath)
    {
        // The bundled Windows FFmpeg binaries depend on sibling MinGW DLLs.
        // LoadLibraryEx with altered search path keeps resolution scoped to this runtime folder.
        var handle = LoadLibraryEx(candidatePath, nint.Zero, LoadWithAlteredSearchPath);
        if (handle != nint.Zero)
            return handle;

        throw new DllNotFoundException($"Unable to load DLL '{candidatePath}': Windows error {Marshal.GetLastWin32Error()}");
    }

    private static IEnumerable<string> EnumerateLibraryCandidates(string rootPath, string libraryName)
    {
        if (Path.IsPathRooted(libraryName))
        {
            yield return libraryName;
            yield break;
        }

        yield return Path.Combine(rootPath, libraryName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                yield return Path.Combine(rootPath, libraryName + ".dll");

            var versionSeparatorIndex = libraryName.IndexOf('.');
            if (versionSeparatorIndex > 0)
            {
                var windowsStyleName = libraryName[..versionSeparatorIndex] + "-" + libraryName[(versionSeparatorIndex + 1)..];
                yield return Path.Combine(rootPath, windowsStyleName);
                yield return Path.Combine(rootPath, windowsStyleName + ".dll");
            }

            var libraryBaseName = Path.GetFileNameWithoutExtension(libraryName);
            foreach (var versionedCandidate in EnumerateVersionedWindowsLibraryCandidates(rootPath, libraryBaseName))
                yield return versionedCandidate;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!libraryName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(rootPath, libraryName + ".dylib");
                yield return Path.Combine(rootPath, "lib" + libraryName + ".dylib");
            }
        }
    }

    private static IEnumerable<string> EnumerateVersionedWindowsLibraryCandidates(string rootPath, string libraryBaseName)
    {
        if (string.IsNullOrWhiteSpace(libraryBaseName) || !Directory.Exists(rootPath))
            yield break;

        foreach (var candidatePath in Directory
                     .EnumerateFiles(rootPath, libraryBaseName + "-*.dll")
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            yield return candidatePath;
        }
    }

    private static IEnumerable<string> EnumerateSearchRoots(string startDirectory)
    {
        var current = Path.GetFullPath(startDirectory);
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                yield break;

            current = parent;
        }
    }

    private static string GetPlatformRuntimeFolder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win-x64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx-x64";

        throw new PlatformNotSupportedException("当前仅支持使用内置 FFmpeg 运行库的 Windows 与 macOS。");
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryEx(string fileName, nint reserved, uint flags);
}
