using FCRevolution.Emulation.Host;

namespace FCRevolution.Core.Workbench.ViewModels;

public sealed class CoreWorkbenchCatalogEntryViewModel
{
    public CoreWorkbenchCatalogEntryViewModel(ManagedCoreCatalogEntry entry)
    {
        Manifest = entry.Manifest;
        CoreId = entry.Manifest.CoreId;
        DisplayName = entry.Manifest.DisplayName;
        SystemId = entry.Manifest.SystemId;
        Version = entry.Manifest.Version;
        BinaryKind = entry.Manifest.BinaryKind;
        SourceKind = entry.SourceKind;
        SourceLabel = entry.SourceKind switch
        {
            ManagedCoreCatalogSourceKind.ProbeDirectory => "probe-directory",
            ManagedCoreCatalogSourceKind.InstalledPackage => "installed-package",
            ManagedCoreCatalogSourceKind.BundledPackage => "bundled-package",
            _ => entry.SourceKind.ToString()
        };
        EntryPath = entry.EntryPath;
        ManifestPath = entry.ManifestPath;
        InstallDirectory = entry.InstallDirectory;
        IsLoadSupported = entry.IsLoadSupported;
        LoadSupportReason = entry.LoadSupportReason;
        LoaderStatusLabel = entry.IsLoadSupported ? "ready" : "missing";
    }

    public FCRevolution.Emulation.Abstractions.CoreManifest Manifest { get; }

    public string CoreId { get; }

    public string DisplayName { get; }

    public string SystemId { get; }

    public string Version { get; }

    public string BinaryKind { get; }

    public ManagedCoreCatalogSourceKind SourceKind { get; }

    public string SourceLabel { get; }

    public string? EntryPath { get; }

    public string? ManifestPath { get; }

    public string? InstallDirectory { get; }

    public bool IsLoadSupported { get; }

    public string? LoadSupportReason { get; }

    public string LoaderStatusLabel { get; }

    public string Summary =>
        $"""
        Core ID: {CoreId}
        Display Name: {DisplayName}
        System: {SystemId}
        Version: {Version}
        Binary Kind: {BinaryKind}
        Source: {SourceLabel}
        Loader: {LoaderStatusLabel}
        Entry Path: {EntryPath ?? "(none)"}
        Manifest Path: {ManifestPath ?? "(none)"}
        Install Directory: {InstallDirectory ?? "(none)"}
        Loader Detail: {LoadSupportReason ?? "(ready)"}
        Supported Media: {(Manifest.SupportedMediaFilePatterns.Count == 0 ? "(none)" : string.Join(", ", Manifest.SupportedMediaFilePatterns))}
        """;
}
