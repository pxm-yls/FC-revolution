namespace FC_Revolution.UI.ViewModels;

internal readonly record struct MainWindowResourceCleanupSelectionState(
    bool CleanupPreviewAnimationsSelected,
    bool CleanupThumbnailsSelected,
    bool CleanupTimelineSavesSelected,
    bool CleanupExportVideosSelected)
{
    public bool HasSelectedCleanupTargets =>
        CleanupPreviewAnimationsSelected ||
        CleanupThumbnailsSelected ||
        CleanupTimelineSavesSelected ||
        CleanupExportVideosSelected;

    public ResourceCleanupSelection ToSelection() =>
        new(
            CleanupPreviewAnimationsSelected,
            CleanupThumbnailsSelected,
            CleanupTimelineSavesSelected,
            CleanupExportVideosSelected);
}

internal static class MainWindowResourceCleanupSelectionController
{
    public static MainWindowResourceCleanupSelectionState Build(
        bool cleanupPreviewAnimationsSelected,
        bool cleanupThumbnailsSelected,
        bool cleanupTimelineSavesSelected,
        bool cleanupExportVideosSelected) =>
        new(
            cleanupPreviewAnimationsSelected,
            cleanupThumbnailsSelected,
            cleanupTimelineSavesSelected,
            cleanupExportVideosSelected);

    public static MainWindowResourceCleanupSelectionState SelectAll() =>
        new(
            CleanupPreviewAnimationsSelected: true,
            CleanupThumbnailsSelected: true,
            CleanupTimelineSavesSelected: true,
            CleanupExportVideosSelected: true);

    public static MainWindowResourceCleanupSelectionState ClearAll() =>
        new(
            CleanupPreviewAnimationsSelected: false,
            CleanupThumbnailsSelected: false,
            CleanupTimelineSavesSelected: false,
            CleanupExportVideosSelected: false);
}
