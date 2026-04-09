using System.Security.Cryptography;
using System.Text;

namespace FCRevolution.Storage;

internal static class AppStorageKeyPolicy
{
    public static string GetRomObjectKey(string romPath)
    {
        var extension = Path.GetExtension(romPath);
        return $"{SanitizeFileName(Path.GetFileNameWithoutExtension(romPath))}-{GetStablePathHash(romPath)[..12]}{extension}";
    }

    public static string GetPreviewVideoObjectKey(string romPath)
    {
        return $"{Path.GetFileName(AppStorageLayoutPolicy.BuildPreviewArtifactBasePath(string.Empty, romPath))}.mp4";
    }

    public static string GetPreviewVideoObjectKey(string romPath, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".mp4";

        return $"{Path.GetFileName(AppStorageLayoutPolicy.BuildPreviewArtifactBasePath(string.Empty, romPath))}{extension.ToLowerInvariant()}";
    }

    public static string GetRomConfigObjectKey(string romPath)
    {
        return Path.Combine("rom-profiles", $"{GetStablePathHash(romPath)}.fcr");
    }

    public static string GetRomImageObjectPrefix(string romPath)
    {
        return Path.Combine(
            "roms",
            $"{SanitizeFileName(Path.GetFileNameWithoutExtension(romPath))}-{GetStablePathHash(romPath)[..12]}");
    }

    public static string GetRomImageObjectKey(string romPath, string imageRole, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        return Path.Combine(
            GetRomImageObjectPrefix(romPath),
            $"{SanitizeFileName(imageRole)}{extension.ToLowerInvariant()}");
    }

    public static string GetSaveNamespace(string romPath)
    {
        return $"rom-{GetStablePathHash(romPath)[..16]}";
    }

    public static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "resource";

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
            builder.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);

        return builder.ToString().Trim().Trim('.');
    }

    public static string GetStablePathHash(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
