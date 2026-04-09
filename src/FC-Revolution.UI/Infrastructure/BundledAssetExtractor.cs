using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FC_Revolution.UI.Infrastructure;

internal static class BundledAssetExtractor
{
    private const string ResourcePrefix = "FCRevolutionBundledAsset/";
    private static readonly object SyncRoot = new();
    private static string? _extractionRoot;

    public static string? TryExtractDirectory(string relativeDirectory)
    {
        var normalizedDirectory = NormalizeRelativePath(relativeDirectory).TrimEnd('/') + "/";
        var assembly = typeof(BundledAssetExtractor).Assembly;
        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix + normalizedDirectory, StringComparison.Ordinal))
            .ToArray();

        if (resourceNames.Length == 0)
            return null;

        var extractionRoot = GetExtractionRoot();
        foreach (var resourceName in resourceNames)
            ExtractResourceIfNeeded(assembly, resourceName, extractionRoot);

        return Path.Combine(extractionRoot, normalizedDirectory.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar));
    }

    public static string? TryExtractFile(string relativePath, bool executable = false)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        var resourceName = ResourcePrefix + normalizedPath;
        var assembly = typeof(BundledAssetExtractor).Assembly;

        if (assembly.GetManifestResourceInfo(resourceName) == null)
            return null;

        var extractionRoot = GetExtractionRoot();
        var filePath = ExtractResourceIfNeeded(assembly, resourceName, extractionRoot);
        if (executable)
            EnsureExecutable(filePath);

        return filePath;
    }

    private static string ExtractResourceIfNeeded(Assembly assembly, string resourceName, string extractionRoot)
    {
        var relativePath = resourceName[ResourcePrefix.Length..];
        var outputPath = Path.Combine(extractionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        lock (SyncRoot)
        {
            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                return outputPath;

            using var resourceStream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("未找到内嵌资源。", resourceName);
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            resourceStream.CopyTo(fileStream);
        }

        return outputPath;
    }

    private static string GetExtractionRoot()
    {
        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_extractionRoot))
                return _extractionRoot;

            var assembly = typeof(BundledAssetExtractor).Assembly;
            var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
            var moduleId = assembly.ManifestModule.ModuleVersionId.ToString("N");
            _extractionRoot = Path.Combine(Path.GetTempPath(), "FC-Revolution", "bundled-assets", $"{version}-{moduleId}");
            Directory.CreateDirectory(_extractionRoot);
            return _extractionRoot;
        }
    }

    private static void EnsureExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(path))
            return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            mode |= UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
            mode |= UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
        catch
        {
        }
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}
