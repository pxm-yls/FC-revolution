using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Models;

public sealed class RomConfigProfile
{
    public const int CurrentFcrVersion = 3;
    public const string ProfileKindValue = "FC-Revolution-ROM-Config";
    private const string InstanceIdFileName = ".fc-revolution.instance";

    public int FcrVersion { get; set; } = CurrentFcrVersion;

    public string ProfileKind { get; set; } = ProfileKindValue;

    public string MachineFingerprint { get; set; } = GetCurrentMachineFingerprint();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool AutoApplyModifiedMemoryOnLaunch { get; set; } = true;

    public Dictionary<string, string> InputOverrides { get; set; } = new();

    public Dictionary<string, Dictionary<string, string>> PlayerInputOverrides { get; set; } = new();

    public List<ExtraInputBindingProfile> ExtraInputBindings { get; set; } = [];

    public List<RomConfigMemoryEntry> ModifiedMemory { get; set; } = [];

    public RomResourceManifest Resources { get; set; } = new();

    public Dictionary<string, string> Metadata { get; set; } = new();

    public static string GetProfilePath(string romPath) => AppObjectStorage.GetRomProfilePath(romPath);

    public static RomConfigProfile Load(string romPath)
        => LoadValidated(romPath).Profile;

    public static RomConfigProfile EnsureResourceManifest(string romPath)
    {
        var result = LoadValidated(romPath);
        if (result.Profile.Resources.IsEmpty)
            Save(romPath, result.Profile);

        return result.Profile;
    }

    public static void RegisterAdditionalObject(string romPath, string resourceName, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(objectKey))
            return;

        var profile = Load(romPath);
        profile.Resources ??= new RomResourceManifest();
        profile.Resources.AdditionalObjects[resourceName] = objectKey;
        Save(romPath, profile);
    }

    public static void RegisterPreviewVideoObject(string romPath, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return;

        var profile = Load(romPath);
        profile.Resources ??= new RomResourceManifest();
        profile.Resources.PreviewVideoObjectKey = objectKey;
        profile.Resources.AdditionalObjects["preview.video"] = objectKey;
        Save(romPath, profile);
    }

    public static void RemoveAdditionalObject(string romPath, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            return;

        var profile = Load(romPath);
        if (profile.Resources?.AdditionalObjects.Remove(resourceName) == true)
            Save(romPath, profile);
    }

    public static IReadOnlyDictionary<string, string> GetAdditionalObjects(string romPath)
    {
        return Load(romPath).Resources?.AdditionalObjects ?? new Dictionary<string, string>();
    }

    public static RomConfigLoadResult LoadValidated(string romPath)
    {
        var profilePath = GetProfilePath(romPath);
        if (!File.Exists(profilePath))
        {
            var legacyPath = AppObjectStorage.GetLegacyRomProfilePath(romPath);
            if (File.Exists(legacyPath))
                profilePath = legacyPath;
        }

        if (!File.Exists(profilePath))
            return new RomConfigLoadResult(new RomConfigProfile(), false, false, false);

        try
        {
            var json = File.ReadAllText(profilePath);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null)
                return new RomConfigLoadResult(new RomConfigProfile(), false, false, false);

            var version = root["fcrVersion"]?.GetValue<int?>() ?? 0;
            var migrated = false;
            var profileKind = root["profileKind"]?.GetValue<string>() ?? "";
            var machineFingerprint = root["machineFingerprint"]?.GetValue<string>() ?? "";
            var createdAtUtc = root["createdAtUtc"]?.GetValue<DateTime?>() ?? DateTime.MinValue;
            var lastUpdatedAtUtc = root["lastUpdatedAtUtc"]?.GetValue<DateTime?>() ?? DateTime.MinValue;

            if (version <= 0)
            {
                root["fcrVersion"] = CurrentFcrVersion;
                version = CurrentFcrVersion;
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(profileKind))
            {
                root["profileKind"] = ProfileKindValue;
                profileKind = ProfileKindValue;
                migrated = true;
            }

            if (string.IsNullOrWhiteSpace(machineFingerprint))
            {
                root["machineFingerprint"] = GetCurrentMachineFingerprint();
                machineFingerprint = root["machineFingerprint"]?.GetValue<string>() ?? "";
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

            if (version > CurrentFcrVersion)
            {
                var futureProfile = JsonSerializer.Deserialize<RomConfigProfile>(root.ToJsonString());
                if (futureProfile == null)
                    return new RomConfigLoadResult(new RomConfigProfile(), false, false, true);

                futureProfile.FcrVersion = version;
                return new RomConfigLoadResult(
                    futureProfile,
                    !string.Equals(futureProfile.ProfileKind, ProfileKindValue, StringComparison.Ordinal),
                    !string.Equals(futureProfile.MachineFingerprint, GetCurrentMachineFingerprint(), StringComparison.OrdinalIgnoreCase),
                    true);
            }

            var profile = JsonSerializer.Deserialize<RomConfigProfile>(root.ToJsonString()) ?? new RomConfigProfile();
            profile.FcrVersion = CurrentFcrVersion;
            profile.ProfileKind = string.IsNullOrWhiteSpace(profile.ProfileKind) ? ProfileKindValue : profile.ProfileKind;
            profile.MachineFingerprint = string.IsNullOrWhiteSpace(profile.MachineFingerprint)
                ? GetCurrentMachineFingerprint()
                : profile.MachineFingerprint;
            profile.CreatedAtUtc = profile.CreatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.CreatedAtUtc;
            profile.LastUpdatedAtUtc = profile.LastUpdatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.LastUpdatedAtUtc;
            profile.ExtraInputBindings ??= [];
            migrated |= MigrateLegacyInputOverrides(profile);
            migrated |= SyncResourceManifest(romPath, profile);

            if (migrated)
                Save(romPath, profile);

            return new RomConfigLoadResult(
                profile,
                !string.Equals(profile.ProfileKind, ProfileKindValue, StringComparison.Ordinal),
                !string.Equals(profile.MachineFingerprint, GetCurrentMachineFingerprint(), StringComparison.OrdinalIgnoreCase),
                false);
        }
        catch
        {
            return new RomConfigLoadResult(new RomConfigProfile(), false, false, false);
        }
    }

    public static void Save(string romPath, RomConfigProfile profile)
    {
        profile.FcrVersion = CurrentFcrVersion;
        profile.ProfileKind = ProfileKindValue;
        profile.MachineFingerprint = GetCurrentMachineFingerprint();
        profile.CreatedAtUtc = profile.CreatedAtUtc == DateTime.MinValue ? DateTime.UtcNow : profile.CreatedAtUtc;
        profile.LastUpdatedAtUtc = DateTime.UtcNow;
        profile.ExtraInputBindings ??= [];
        SyncResourceManifest(romPath, profile);
        Directory.CreateDirectory(Path.GetDirectoryName(GetProfilePath(romPath))!);
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetProfilePath(romPath), json);
    }

    public static void SavePreviewMetadata(string romPath, bool isAnimated, int intervalMs, int frameCount)
    {
        var profile = Load(romPath);
        var resources = profile.Resources ??= new RomResourceManifest();
        resources.PreviewIsAnimated = isAnimated;
        resources.PreviewIntervalMs = isAnimated ? intervalMs : 0;
        resources.PreviewFrameCount = isAnimated ? frameCount : 0;
        Save(romPath, profile);
    }

    public static void TrustCurrentMachine(string romPath)
    {
        var profile = Load(romPath);
        profile.MachineFingerprint = GetCurrentMachineFingerprint();
        Save(romPath, profile);
    }

    public static string GetCurrentMachineFingerprint()
    {
        var raw = $"FC-Revolution|{GetOrCreateLocalInstanceId()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..12];
    }

    private static string GetOrCreateLocalInstanceId()
    {
        var primaryPath = AppObjectStorage.GetInstanceIdPath();
        if (TryReadOrCreateInstanceId(primaryPath, out var instanceId))
            return instanceId;

        return Guid.NewGuid().ToString("N");
    }

    private static bool TryReadOrCreateInstanceId(string path, out string instanceId)
    {
        try
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            if (File.Exists(path))
            {
                instanceId = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(instanceId))
                    return true;
            }

            instanceId = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, instanceId);
            return true;
        }
        catch
        {
            instanceId = "";
            return false;
        }
    }

    private static bool MigrateLegacyInputOverrides(RomConfigProfile profile)
    {
        profile.PlayerInputOverrides ??= new Dictionary<string, Dictionary<string, string>>();
        profile.InputOverrides ??= new Dictionary<string, string>();
        profile.ExtraInputBindings ??= [];

        if (profile.PlayerInputOverrides.Count > 0 || profile.InputOverrides.Count == 0)
            return false;

        profile.PlayerInputOverrides["Player1"] = new Dictionary<string, string>(profile.InputOverrides);
        return true;
    }

    private static bool SyncResourceManifest(string romPath, RomConfigProfile profile)
    {
        var changed = false;
        var resources = profile.Resources ??= new RomResourceManifest();

        changed |= SetIfDifferent(resources.RomObjectKey, value => resources.RomObjectKey = value, AppObjectStorage.GetRomObjectKey(romPath));
        changed |= SetIfDifferent(resources.ConfigObjectKey, value => resources.ConfigObjectKey = value, AppObjectStorage.GetRomConfigObjectKey(romPath));
        changed |= SetIfDifferent(resources.SaveNamespace, value => resources.SaveNamespace = value, AppObjectStorage.GetSaveNamespace(romPath));
        changed |= SetIfDifferent(resources.ImageObjectPrefix, value => resources.ImageObjectPrefix = value, AppObjectStorage.GetRomImageObjectPrefix(romPath));
        changed |= SyncPreviewVideoObjectKey(romPath, resources);

        return changed;
    }

    private static bool SyncPreviewVideoObjectKey(string romPath, RomResourceManifest resources)
    {
        resources.AdditionalObjects ??= new Dictionary<string, string>();

        var resolvedObjectKey = ResolvePreviewVideoObjectKey(romPath, resources);
        var changed = false;

        if (!string.Equals(resources.PreviewVideoObjectKey, resolvedObjectKey, StringComparison.Ordinal))
        {
            resources.PreviewVideoObjectKey = resolvedObjectKey;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(resolvedObjectKey))
        {
            if (resources.AdditionalObjects.Remove("preview.video"))
                changed = true;

            return changed;
        }

        if (!resources.AdditionalObjects.TryGetValue("preview.video", out var registeredKey) ||
            !string.Equals(registeredKey, resolvedObjectKey, StringComparison.Ordinal))
        {
            resources.AdditionalObjects["preview.video"] = resolvedObjectKey;
            changed = true;
        }

        return changed;
    }

    private static string ResolvePreviewVideoObjectKey(string romPath, RomResourceManifest resources)
    {
        var candidates = new List<string>();
        if (resources.AdditionalObjects.TryGetValue("preview.video", out var additionalKey) && !string.IsNullOrWhiteSpace(additionalKey))
            candidates.Add(additionalKey);
        if (!string.IsNullOrWhiteSpace(resources.PreviewVideoObjectKey))
            candidates.Add(resources.PreviewVideoObjectKey);

        var defaultKey = AppObjectStorage.GetPreviewVideoObjectKey(romPath);
        if (!string.IsNullOrWhiteSpace(defaultKey))
            candidates.Add(defaultKey);

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            var candidatePath = AppObjectStorage.Default.GetObjectPath(ObjectStorageBucket.PreviewVideos, candidate);
            if (File.Exists(candidatePath))
                return candidate;
        }

        var prefix = Path.GetFileNameWithoutExtension(resources.RomObjectKey);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var matchedPath = Directory
                .EnumerateFiles(AppObjectStorage.GetPreviewVideosDirectory(), $"{prefix}*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(matchedPath))
                return AppObjectStorage.Default.GetObjectKey(ObjectStorageBucket.PreviewVideos, matchedPath);
        }

        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? "";
    }

    private static bool SetIfDifferent(string currentValue, Action<string> setter, string nextValue)
    {
        if (string.Equals(currentValue, nextValue, StringComparison.Ordinal))
            return false;

        setter(nextValue);
        return true;
    }
}

public sealed class RomResourceManifest
{
    public string RomObjectKey { get; set; } = "";

    public string PreviewVideoObjectKey { get; set; } = "";

    public string ConfigObjectKey { get; set; } = "";

    public string SaveNamespace { get; set; } = "";

    public string ImageObjectPrefix { get; set; } = "";

    public Dictionary<string, string> AdditionalObjects { get; set; } = new();

    public bool? PreviewIsAnimated { get; set; }

    public int PreviewIntervalMs { get; set; }

    public int PreviewFrameCount { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(RomObjectKey) &&
        string.IsNullOrWhiteSpace(PreviewVideoObjectKey) &&
        string.IsNullOrWhiteSpace(ConfigObjectKey) &&
        string.IsNullOrWhiteSpace(SaveNamespace) &&
        string.IsNullOrWhiteSpace(ImageObjectPrefix) &&
        AdditionalObjects.Count == 0;
}

public sealed class RomConfigMemoryEntry
{
    public string Address { get; set; } = "0000";

    public string Value { get; set; } = "00";

    public bool IsLocked { get; set; }
}

public sealed record RomConfigLoadResult(
    RomConfigProfile Profile,
    bool HasProfileKindMismatch,
    bool IsForeignMachineProfile,
    bool IsFutureVersionProfile);
