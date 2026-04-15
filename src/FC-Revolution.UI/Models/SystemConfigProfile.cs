using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Models;

public sealed class SystemConfigProfile
{
    public const int DefaultLanArcadePort = 11778;
    public const int CurrentFcrVersion = RomConfigProfile.CurrentFcrVersion;
    public const string ProfileKindValue = "FC-Revolution-System-Config";
    private const int EmulatorFramesPerSecond = 60;
    private const double MinShortRewindSeconds = 1d;
    private const double MaxShortRewindSeconds = 30d;
    private List<string> _coreProbePaths = [];
    private Dictionary<string, Dictionary<string, string>> _portInputOverrides = new(StringComparer.Ordinal);
    public int FcrVersion { get; set; } = CurrentFcrVersion;

    public string ProfileKind { get; set; } = ProfileKindValue;

    public string MachineFingerprint { get; set; } = RomConfigProfile.GetCurrentMachineFingerprint();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public double Volume { get; set; } = 2.0;

    public bool ShowDebugStatus { get; set; }

    public int MaxConcurrentGameWindows { get; set; } = 4;

    public int PreviewGenerationParallelism { get; set; } = 1;

    public int PreviewGenerationSpeedMultiplier { get; set; } = 3;

    public double PreviewResolutionScale { get; set; } = 1.0;

    public int PreviewPreloadWindowSeconds { get; set; } = 3;

    public string PreviewEncodingMode { get; set; } = global::FC_Revolution.UI.Models.PreviewEncodingMode.Auto.ToString();

    public string LayoutMode { get; set; } = "BookShelf";

    public string SortField { get; set; } = "Name";

    public bool SortDescending { get; set; }

    public string GameAspectRatioMode { get; set; } = "Native";

    public string DefaultCoreId { get; set; } = string.Empty;

    public List<string> CoreProbePaths
    {
        get => _coreProbePaths;
        set => _coreProbePaths = NormalizeCoreProbePaths(value);
    }

    public string MacRenderUpscaleMode { get; set; } = "None";

    public string MacRenderUpscaleOutputResolution { get; set; } = "Hd1080";

    public string TimelineMode { get; set; } = TimelineModeOption.FullTimeline.ToString();

    public double ShortRewindSeconds { get; set; } = 5.0;

    public int ShortRewindFrames { get; set; } = 5 * EmulatorFramesPerSecond;

    public string ResourceRootPath { get; set; } = AppObjectStorage.GetDefaultResourceRoot();

    public bool IsLanArcadeEnabled { get; set; }

    public int LanArcadePort { get; set; } = DefaultLanArcadePort;

    public bool IsLanArcadeWebPadEnabled { get; set; } = true;

    public bool IsLanArcadeDebugPagesEnabled { get; set; }

    /// <summary>串流分辨率倍率，1=256×240，2=512×480，3=768×720，默认 2</summary>
    public int LanStreamScaleMultiplier { get; set; } = 2;

    /// <summary>串流 JPEG 编码质量，范围 60–95，默认 85</summary>
    public int LanStreamJpegQuality { get; set; } = 85;

    /// <summary>串流画面锐化模式（None/Subtle/Standard/Strong）</summary>
    public string LanStreamSharpenMode { get; set; } = "None";

    /// <summary>本地游戏窗口画质增强模式（None/SubtleSharpen/CrtScanlines/SoftBlur/VividColor）</summary>
    public string LocalDisplayEnhancementMode { get; set; } = "None";

    public DebugWindowDisplaySettingsProfile DebugWindowDisplaySettings { get; set; } = new();

    public Dictionary<string, Dictionary<string, string>> PortInputOverrides
    {
        get => _portInputOverrides;
        set => _portInputOverrides = NormalizePortInputOverrides(value);
    }

    public List<ExtraInputBindingProfile> ExtraInputBindings { get; set; } = [];

    public Dictionary<string, ShortcutBindingProfile> ShortcutBindings { get; set; } = new();

    public InputBindingLayoutProfile InputBindingLayout { get; set; } = InputBindingLayoutProfile.CreateDefault();

    public static string GetProfilePath()
    {
        return AppObjectStorage.GetBootstrapSystemConfigPath();
    }

    public static SystemConfigProfile Load()
    {
        string? path = null;
        foreach (var candidatePath in EnumerateProfileProbePaths())
        {
            if (!File.Exists(candidatePath))
                continue;

            path = candidatePath;
            break;
        }

        if (path == null)
            return new SystemConfigProfile();

        try
        {
            var json = File.ReadAllText(path);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null)
                return new SystemConfigProfile();

            var migrated = false;
            var version = root["fcrVersion"]?.GetValue<int?>() ?? 0;
            var profileKind = root["profileKind"]?.GetValue<string>() ?? "";
            var machineFingerprint = root["machineFingerprint"]?.GetValue<string>() ?? "";
            var createdAtUtc = root["createdAtUtc"]?.GetValue<DateTime?>() ?? DateTime.MinValue;
            var lastUpdatedAtUtc = root["lastUpdatedAtUtc"]?.GetValue<DateTime?>() ?? DateTime.MinValue;

            if (version <= 0)
            {
                root["fcrVersion"] = CurrentFcrVersion;
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(profileKind))
            {
                root["profileKind"] = ProfileKindValue;
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(machineFingerprint))
            {
                root["machineFingerprint"] = RomConfigProfile.GetCurrentMachineFingerprint();
                migrated = true;
            }

            if (createdAtUtc == DateTime.MinValue)
            {
                root["createdAtUtc"] = DateTime.UtcNow;
                migrated = true;
            }

            if (lastUpdatedAtUtc == DateTime.MinValue)
            {
                root["lastUpdatedAtUtc"] = DateTime.UtcNow;
                migrated = true;
            }

            migrated |= MigrateLegacyCoreProbePathAliases(root);
            migrated |= MigrateLegacyPortInputOverrideAliases(root);
            migrated |= MigrateLegacyExtraInputBindingOrdinals(root);

            if ((root["previewGenerationParallelism"]?.GetValue<int?>() ?? 0) <= 0)
            {
                root["previewGenerationParallelism"] = 1;
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(root["macRenderUpscaleMode"]?.GetValue<string>()))
            {
                root["macRenderUpscaleMode"] = "None";
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(root["macRenderUpscaleOutputResolution"]?.GetValue<string>()))
            {
                root["macRenderUpscaleOutputResolution"] = "Hd1080";
                migrated = true;
            }

            var profile = JsonSerializer.Deserialize<SystemConfigProfile>(root.ToJsonString()) ?? new SystemConfigProfile();
            profile.FcrVersion = CurrentFcrVersion;
            profile.ProfileKind = ProfileKindValue;
            profile.MachineFingerprint = RomConfigProfile.GetCurrentMachineFingerprint();
            profile.CreatedAtUtc = profile.CreatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.CreatedAtUtc;
            profile.LastUpdatedAtUtc = profile.LastUpdatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.LastUpdatedAtUtc;
            profile.MaxConcurrentGameWindows = profile.MaxConcurrentGameWindows <= 1
                ? 4
                : Math.Clamp(profile.MaxConcurrentGameWindows, 1, 8);
            profile.PreviewGenerationParallelism = Math.Clamp(profile.PreviewGenerationParallelism, 1, 8);
            profile.PreviewGenerationSpeedMultiplier = Math.Clamp(profile.PreviewGenerationSpeedMultiplier, 1, 10);
            profile.PreviewResolutionScale = Math.Clamp(profile.PreviewResolutionScale <= 0 ? 1.0 : profile.PreviewResolutionScale, 0.5, 1.0);
            profile.PreviewPreloadWindowSeconds = Math.Clamp(profile.PreviewPreloadWindowSeconds, 1, 3);
            if (!Enum.TryParse<global::FC_Revolution.UI.Models.PreviewEncodingMode>(profile.PreviewEncodingMode, out _))
                profile.PreviewEncodingMode = global::FC_Revolution.UI.Models.PreviewEncodingMode.Auto.ToString();
            profile.ShortRewindFrames = profile.ShortRewindFrames > 0
                ? Math.Clamp(
                    profile.ShortRewindFrames,
                    ConvertShortRewindSecondsToFrames(MinShortRewindSeconds),
                    ConvertShortRewindSecondsToFrames(MaxShortRewindSeconds))
                : ConvertShortRewindSecondsToFrames(profile.ShortRewindSeconds);
            profile.ShortRewindSeconds = ConvertShortRewindFramesToSeconds(profile.ShortRewindFrames);
            profile.LanArcadePort = profile.LanArcadePort == 6666
                ? DefaultLanArcadePort
                : Math.Clamp(profile.LanArcadePort, 1024, 65535);
            var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(profile.ResourceRootPath);
            if (!PathsEqual(profile.ResourceRootPath, normalizedResourceRoot))
                migrated = true;
            profile.ResourceRootPath = normalizedResourceRoot;
            profile.LanStreamScaleMultiplier = Math.Clamp(
                profile.LanStreamScaleMultiplier <= 0 ? 2 : profile.LanStreamScaleMultiplier, 1, 3);
            profile.LanStreamJpegQuality = Math.Clamp(
                profile.LanStreamJpegQuality <= 0 ? 85 : profile.LanStreamJpegQuality, 60, 95);
            if (string.IsNullOrWhiteSpace(profile.LanStreamSharpenMode))
                profile.LanStreamSharpenMode = "None";
            if (string.IsNullOrWhiteSpace(profile.LocalDisplayEnhancementMode))
                profile.LocalDisplayEnhancementMode = "None";
            var normalizedDefaultCoreId = string.IsNullOrWhiteSpace(profile.DefaultCoreId)
                ? string.Empty
                : profile.DefaultCoreId.Trim();
            if (!string.Equals(profile.DefaultCoreId, normalizedDefaultCoreId, StringComparison.Ordinal))
                migrated = true;
            profile.DefaultCoreId = normalizedDefaultCoreId;
            var normalizedCoreProbePaths = NormalizeCoreProbePaths(profile.CoreProbePaths);
            if (!PathsEqual(profile.CoreProbePaths, normalizedCoreProbePaths))
                migrated = true;
            profile.CoreProbePaths = normalizedCoreProbePaths;
            if (string.IsNullOrWhiteSpace(profile.MacRenderUpscaleMode))
                profile.MacRenderUpscaleMode = "None";
            if (string.IsNullOrWhiteSpace(profile.MacRenderUpscaleOutputResolution))
                profile.MacRenderUpscaleOutputResolution = "Hd1080";
            profile.DebugWindowDisplaySettings ??= new DebugWindowDisplaySettingsProfile();
            profile.PortInputOverrides = profile.PortInputOverrides;
            profile.ExtraInputBindings ??= [];
            profile.ShortcutBindings ??= new Dictionary<string, ShortcutBindingProfile>();
            profile.InputBindingLayout ??= InputBindingLayoutProfile.CreateDefault();
            profile.InputBindingLayout.Sanitize();

            if (migrated || !PathsEqual(path, GetProfilePath()))
                Save(profile);

            return profile;
        }
        catch
        {
            return new SystemConfigProfile();
        }
    }

    public static void Save(SystemConfigProfile profile)
    {
        profile.FcrVersion = CurrentFcrVersion;
        profile.ProfileKind = ProfileKindValue;
        profile.MachineFingerprint = RomConfigProfile.GetCurrentMachineFingerprint();
        profile.CreatedAtUtc = profile.CreatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.CreatedAtUtc;
        profile.LastUpdatedAtUtc = DateTime.UtcNow;
        profile.MaxConcurrentGameWindows = Math.Clamp(profile.MaxConcurrentGameWindows, 1, 8);
        profile.PreviewGenerationParallelism = Math.Clamp(profile.PreviewGenerationParallelism, 1, 8);
        profile.PreviewGenerationSpeedMultiplier = Math.Clamp(profile.PreviewGenerationSpeedMultiplier, 1, 10);
        profile.PreviewResolutionScale = Math.Clamp(profile.PreviewResolutionScale <= 0 ? 1.0 : profile.PreviewResolutionScale, 0.5, 1.0);
        profile.PreviewPreloadWindowSeconds = Math.Clamp(profile.PreviewPreloadWindowSeconds, 1, 3);
        if (!Enum.TryParse<global::FC_Revolution.UI.Models.PreviewEncodingMode>(profile.PreviewEncodingMode, out _))
            profile.PreviewEncodingMode = global::FC_Revolution.UI.Models.PreviewEncodingMode.Auto.ToString();
        profile.DefaultCoreId = string.IsNullOrWhiteSpace(profile.DefaultCoreId)
            ? string.Empty
            : profile.DefaultCoreId.Trim();
        profile.CoreProbePaths = NormalizeCoreProbePaths(profile.CoreProbePaths);
        profile.ShortRewindFrames = profile.ShortRewindFrames > 0
            ? Math.Clamp(
                profile.ShortRewindFrames,
                ConvertShortRewindSecondsToFrames(MinShortRewindSeconds),
                ConvertShortRewindSecondsToFrames(MaxShortRewindSeconds))
            : ConvertShortRewindSecondsToFrames(profile.ShortRewindSeconds);
        profile.ShortRewindSeconds = ConvertShortRewindFramesToSeconds(profile.ShortRewindFrames);
        profile.LanArcadePort = profile.LanArcadePort == 6666
            ? DefaultLanArcadePort
            : Math.Clamp(profile.LanArcadePort, 1024, 65535);
        profile.ResourceRootPath = AppObjectStorage.NormalizeConfiguredResourceRoot(profile.ResourceRootPath);
        profile.LanStreamScaleMultiplier = Math.Clamp(
            profile.LanStreamScaleMultiplier <= 0 ? 2 : profile.LanStreamScaleMultiplier, 1, 3);
        profile.LanStreamJpegQuality = Math.Clamp(
            profile.LanStreamJpegQuality <= 0 ? 85 : profile.LanStreamJpegQuality, 60, 95);
        if (string.IsNullOrWhiteSpace(profile.LanStreamSharpenMode))
            profile.LanStreamSharpenMode = "None";
        if (string.IsNullOrWhiteSpace(profile.LocalDisplayEnhancementMode))
            profile.LocalDisplayEnhancementMode = "None";
        if (string.IsNullOrWhiteSpace(profile.MacRenderUpscaleMode))
            profile.MacRenderUpscaleMode = "None";
        if (string.IsNullOrWhiteSpace(profile.MacRenderUpscaleOutputResolution))
            profile.MacRenderUpscaleOutputResolution = "Hd1080";
        profile.DebugWindowDisplaySettings ??= new DebugWindowDisplaySettingsProfile();
        profile.PortInputOverrides = profile.PortInputOverrides;
        profile.ExtraInputBindings ??= [];
        profile.ShortcutBindings ??= new Dictionary<string, ShortcutBindingProfile>();
        profile.InputBindingLayout ??= InputBindingLayoutProfile.CreateDefault();
        profile.InputBindingLayout.Sanitize();
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });

        foreach (var path in GetWritePaths(profile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }

    public IReadOnlyList<string> GetEffectiveCoreProbePaths() =>
        ResolveCoreProbePaths(ResourceRootPath, CoreProbePaths);

    public IReadOnlyList<string> GetEffectiveCoreProbeDirectories(string? appBaseDirectory = null) =>
        ResolveEffectiveCoreProbeDirectories(ResourceRootPath, CoreProbePaths, appBaseDirectory);

    public static IReadOnlyList<string> ResolveCoreProbePaths(
        string? resourceRootPath,
        IEnumerable<string>? configuredProbePaths = null)
    {
        var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);
        return DistinctPaths(
            [
                AppObjectStorage.GetDevelopmentCoreModulesDirectory(normalizedResourceRoot),
                .. NormalizeCoreProbePaths(configuredProbePaths)
            ])
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveEffectiveCoreProbeDirectories(
        string? resourceRootPath,
        IEnumerable<string>? configuredProbePaths = null,
        string? appBaseDirectory = null)
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(GetPathComparer());

        void AddPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var normalized = Path.GetFullPath(path);
            if (seen.Add(normalized))
                directories.Add(normalized);
        }

        var normalizedAppBaseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory;
        AddPath(Path.Combine(normalizedAppBaseDirectory, "cores", "managed"));
        foreach (var path in ResolveCoreProbePaths(resourceRootPath, configuredProbePaths))
            AddPath(path);

        return directories;
    }
    private static IEnumerable<string> EnumerateProfileProbePaths()
    {
        if (AppObjectStorage.HasEnvironmentResourceRootOverride())
        {
            return DistinctPaths(
            [
                GetProfilePath(),
                AppObjectStorage.GetSystemConfigPath()
            ]);
        }

        return DistinctPaths(
        [
            GetProfilePath(),
            AppObjectStorage.GetSystemConfigPath(),
            AppObjectStorage.GetLegacyInstalledSystemConfigPath(),
            AppObjectStorage.GetLegacySystemConfigPath()
        ]);
    }

    private static IEnumerable<string> GetWritePaths(SystemConfigProfile profile)
    {
        return DistinctPaths(
        [
            GetProfilePath(),
            AppObjectStorage.GetSystemConfigPath(profile.ResourceRootPath)
        ]);
    }

    private static IEnumerable<string> DistinctPaths(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(GetPathComparer());
        foreach (var path in paths)
        {
            var normalized = Path.GetFullPath(path);
            if (seen.Add(normalized))
                yield return normalized;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool PathsEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (left == null || right == null)
            return left == right;

        if (left.Count != right.Count)
            return false;

        var comparer = GetPathComparer();
        for (var index = 0; index < left.Count; index++)
        {
            if (!comparer.Equals(left[index], right[index]))
                return false;
        }

        return true;
    }

    private static bool MigrateLegacyCoreProbePathAliases(JsonObject root)
    {
        if (FindProperty(root, "coreProbePaths", "CoreProbePaths") is not null ||
            FindProperty(root, "managedCoreProbePaths", "ManagedCoreProbePaths") is not { } legacyProbePaths)
        {
            return false;
        }

        root[nameof(CoreProbePaths)] = legacyProbePaths.DeepClone();
        return true;
    }

    private static bool MigrateLegacyPortInputOverrideAliases(JsonObject root)
    {
        if (FindProperty(root, "portInputOverrides", "PortInputOverrides") is not null ||
            FindProperty(root, "playerInputOverrides", "PlayerInputOverrides") is not { } legacyPortOverrides)
        {
            return false;
        }

        root[nameof(PortInputOverrides)] = legacyPortOverrides.DeepClone();
        return true;
    }

    private static bool MigrateLegacyExtraInputBindingOrdinals(JsonObject root)
    {
        var migrated = false;
        JsonArray extraBindings;
        if (FindProperty(root, nameof(ExtraInputBindings)) is JsonArray currentExtraBindings)
        {
            extraBindings = currentExtraBindings;
        }
        else if (FindProperty(root, "extraInputBindings") is JsonArray legacyExtraBindings)
        {
            root[nameof(ExtraInputBindings)] = legacyExtraBindings.DeepClone();
            extraBindings = (JsonArray)root[nameof(ExtraInputBindings)]!;
            migrated = true;
        }
        else
        {
            return false;
        }

        foreach (var node in extraBindings)
        {
            if (node is not JsonObject binding ||
                FindProperty(binding, "legacyPortOrdinal", "LegacyPortOrdinal") is not null ||
                FindProperty(binding, "player", "Player") is not { } legacyPlayer)
            {
                continue;
            }

            binding[nameof(ExtraInputBindingProfile.LegacyPortOrdinal)] = legacyPlayer.DeepClone();
            migrated = true;
        }

        return migrated;
    }

    private static JsonNode? FindProperty(JsonObject root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var value))
                return value;
        }

        return null;
    }

    private static List<string> NormalizeCoreProbePaths(IEnumerable<string>? paths)
    {
        var normalizedPaths = new List<string>();
        if (paths == null)
            return normalizedPaths;

        var seen = new HashSet<string>(GetPathComparer());
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var normalized = Path.GetFullPath(path.Trim());
            if (seen.Add(normalized))
                normalizedPaths.Add(normalized);
        }

        return normalizedPaths;
    }

    private static Dictionary<string, Dictionary<string, string>> NormalizePortInputOverrides(
        Dictionary<string, Dictionary<string, string>>? source)
    {
        var normalized = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (source == null)
            return normalized;

        foreach (var portEntry in source)
        {
            if (string.IsNullOrWhiteSpace(portEntry.Key))
                continue;

            normalized[portEntry.Key.Trim()] = portEntry.Value?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return normalized;
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static int ConvertShortRewindSecondsToFrames(double seconds)
    {
        var normalizedSeconds = double.IsFinite(seconds)
            ? Math.Clamp(seconds, MinShortRewindSeconds, MaxShortRewindSeconds)
            : 5.0;
        return Math.Max(1, (int)Math.Round(normalizedSeconds * EmulatorFramesPerSecond, MidpointRounding.AwayFromZero));
    }

    private static double ConvertShortRewindFramesToSeconds(int frames)
    {
        return Math.Round(Math.Max(1, frames) / (double)EmulatorFramesPerSecond, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed class DebugWindowDisplaySettingsProfile
{
    public bool ShowRegisters { get; set; }

    public bool ShowPpu { get; set; }

    public bool ShowDisasm { get; set; }

    public bool ShowStack { get; set; }

    public bool ShowZeroPage { get; set; }

    public bool ShowMemoryEditor { get; set; } = true;

    public bool ShowMemoryPage { get; set; } = true;

    public bool ShowModifiedMemory { get; set; } = true;

    public DebugWindowDisplaySettingsProfile Clone() => new()
    {
        ShowRegisters = ShowRegisters,
        ShowPpu = ShowPpu,
        ShowDisasm = ShowDisasm,
        ShowStack = ShowStack,
        ShowZeroPage = ShowZeroPage,
        ShowMemoryEditor = ShowMemoryEditor,
        ShowMemoryPage = ShowMemoryPage,
        ShowModifiedMemory = ShowModifiedMemory
    };

    public static DebugWindowDisplaySettingsProfile Sanitize(DebugWindowDisplaySettingsProfile? settings) => new()
    {
        ShowRegisters = settings?.ShowRegisters ?? false,
        ShowPpu = settings?.ShowPpu ?? false,
        ShowDisasm = settings?.ShowDisasm ?? false,
        ShowStack = settings?.ShowStack ?? false,
        ShowZeroPage = settings?.ShowZeroPage ?? false,
        ShowMemoryEditor = settings?.ShowMemoryEditor ?? true,
        ShowMemoryPage = settings?.ShowMemoryPage ?? true,
        ShowModifiedMemory = settings?.ShowModifiedMemory ?? true
    };
}
