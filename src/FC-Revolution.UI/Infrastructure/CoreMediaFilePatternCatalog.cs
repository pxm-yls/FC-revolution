using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class CoreMediaFilePatternCatalog
{
    private static readonly IReadOnlyList<string> DefaultPatterns = ["*.nes"];

    public static IReadOnlyList<string> ResolvePatterns(IEnumerable<CoreManifest> manifests) =>
        ResolvePatterns(manifests.SelectMany(static manifest => manifest.SupportedMediaFilePatterns));

    public static IReadOnlyList<string> ResolvePatterns(IEnumerable<string>? patterns)
    {
        var normalized = (patterns ?? [])
            .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(static pattern => NormalizePattern(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? DefaultPatterns : normalized;
    }

    public static IReadOnlyList<string> EnumerateFiles(
        string directoryPath,
        IReadOnlyList<string> supportedFilePatterns,
        SearchOption searchOption)
    {
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var files = new HashSet<string>(pathComparer);

        foreach (var pattern in ResolvePatterns(supportedFilePatterns))
        {
            IEnumerable<string> matches;
            try
            {
                matches = Directory.EnumerateFiles(directoryPath, pattern, searchOption);
            }
            catch (DirectoryNotFoundException)
            {
                return [];
            }

            foreach (var file in matches)
                files.Add(file);
        }

        return files
            .OrderBy(static path => path, pathComparer)
            .ToList();
    }

    public static string DescribePatterns(IReadOnlyList<string> supportedFilePatterns) =>
        string.Join("、", ResolvePatterns(supportedFilePatterns).Select(static pattern => $"`{pattern}`"));

    private static string NormalizePattern(string pattern)
    {
        var trimmed = pattern.Trim();
        if (trimmed.StartsWith("*.", StringComparison.Ordinal))
            return trimmed;
        if (trimmed.StartsWith(".", StringComparison.Ordinal))
            return $"*{trimmed}";
        if (trimmed.Contains('*'))
            return trimmed;

        return $"*.{trimmed.TrimStart('.')}";
    }
}
