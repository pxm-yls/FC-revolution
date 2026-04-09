namespace FCRevolution.Contracts.Roms;

public sealed record RomSummaryDto(
    string DisplayName,
    string Path,
    bool IsLoaded,
    bool HasPreview,
    string? PreviewUrl = null,
    string? PreviewResolvedPath = null);
