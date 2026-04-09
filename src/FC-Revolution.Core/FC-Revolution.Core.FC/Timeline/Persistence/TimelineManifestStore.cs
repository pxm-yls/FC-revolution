using System.Text.Json;

namespace FCRevolution.Core.Timeline.Persistence;

internal sealed class TimelineManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public TimelineManifest Load(string romId)
    {
        var manifestPath = TimelineStoragePaths.GetManifestPath(romId);
        if (!File.Exists(manifestPath))
            return new TimelineManifest();

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<TimelineManifest>(json, JsonOptions) ?? new TimelineManifest();
    }

    public void Save(TimelineManifest manifest)
    {
        Directory.CreateDirectory(TimelineStoragePaths.GetRomDirectory(manifest.RomId));
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(TimelineStoragePaths.GetManifestPath(manifest.RomId), json);
    }
}
