using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class RomLibraryItem : ObservableObject
{
    private static readonly Regex ImportedNameSuffixRegex = new("-(?:[0-9a-fA-F]{12})$", RegexOptions.Compiled);

    public RomLibraryItem(
        string name,
        string path,
        string previewFilePath,
        bool hasPreview,
        long fileSizeBytes,
        DateTime importedAtUtc,
        bool? cachedPreviewIsAnimated = null,
        int cachedPreviewIntervalMs = 0,
        int cachedPreviewFrameCount = 0)
    {
        Name = name;
        Path = path;
        _previewFilePath = previewFilePath;
        FileSizeBytes = fileSizeBytes;
        ImportedAtUtc = importedAtUtc;
        _hasPreview = hasPreview;
        KnownPreviewIsAnimated = cachedPreviewIsAnimated;
        KnownPreviewIntervalMs = cachedPreviewIntervalMs;
        KnownPreviewFrameCount = cachedPreviewFrameCount;
    }

    public string Name { get; }

    public string Path { get; }

    private string _previewFilePath;

    public string PreviewFilePath => _previewFilePath;

    public long FileSizeBytes { get; }

    public DateTime ImportedAtUtc { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBadge))]
    private bool _hasPreview;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private WriteableBitmap? _currentPreviewBitmap;

    private readonly RomLibraryItemPreviewState _previewState = new();

    public string DisplayName => ImportedNameSuffixRegex.Replace(System.IO.Path.GetFileNameWithoutExtension(Name), "");

    public string ShelfCaption => DisplayName.Length <= 18
        ? DisplayName
        : $"{DisplayName[..16]}…";

    public string PreviewBadge => HasPreview ? "" : "待生成";

    public string SizeLabel => FileSizeBytes >= 1024 * 1024
        ? $"{FileSizeBytes / 1024d / 1024d:F2} MB"
        : $"{FileSizeBytes / 1024d:F0} KB";

    public string ImportedAtLabel => ImportedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public bool? KnownPreviewIsAnimated { get; private set; }

    public int KnownPreviewIntervalMs { get; private set; }

    public int KnownPreviewFrameCount { get; private set; }

    public bool HasPreviewBitmap => CurrentPreviewBitmap != null;

    public bool NoPreviewBitmap => CurrentPreviewBitmap == null;

    public int PreviewFrameCount => _previewState.FrameCount;

    public bool HasLoadedPreview => _previewState.HasLoadedPreview;

    public bool IsPreviewAnimated => _previewState.IsAnimated;

    public bool IsLegacyPreview => _previewState.IsLegacyPreview;

    public bool IsMemoryPreview => _previewState.IsMemoryBacked;

    public bool IsSmoothPlaybackEnabled => _previewState.IsSmoothPlaybackEnabled;

    public bool HasPreviewStreamHandle => _previewState.HasStreamHandle;

    public bool HasPrefetchedPreviewFrame => _previewState.HasPrefetchedFrame;

    public int CachedPreviewBitmapCount => _previewState.CachedBitmapCount;

    public int CachedPreviewFrameCount => _previewState.CachedFrameCount;

    public long EstimatedPreviewBitmapCacheBytes => _previewState.EstimatedBitmapCacheBytes;

    public long EstimatedPreviewFrameCacheBytes => _previewState.EstimatedMemoryFrameBytes;

    public int PreviewIntervalMs => _previewState.IntervalMs;

    public bool SupportsFullFrameCaching => _previewState.SupportsFullFrameCaching;

    public string PreviewDebugInfo => _previewState.DebugInfo;

    public void UpdatePreviewFilePath(string previewFilePath)
    {
        if (string.Equals(_previewFilePath, previewFilePath, StringComparison.OrdinalIgnoreCase))
            return;

        _previewFilePath = previewFilePath;
    }

    public void SetPreviewStream(StreamingPreview preview)
    {
        _previewState.SetPreviewStream(preview);
        KnownPreviewIsAnimated = _previewState.KnownPreviewIsAnimated;
        KnownPreviewIntervalMs = _previewState.KnownPreviewIntervalMs;
        KnownPreviewFrameCount = _previewState.KnownPreviewFrameCount;
        CurrentPreviewBitmap = _previewState.CurrentBitmap;
        NotifyPreviewLifecycleStateChanged();
    }

    public void ClearPreviewFrames()
    {
        _previewState.ClearPreviewFrames();
        CurrentPreviewBitmap = null;
        NotifyPreviewLifecycleStateChanged();
    }

    public void AdvancePreviewFrame()
    {
        if (!_previewState.AdvancePreviewFrame())
            return;

        CurrentPreviewBitmap = _previewState.CurrentBitmap;
        OnPropertyChanged(nameof(CurrentPreviewBitmap));
        OnPropertyChanged(nameof(PreviewDebugInfo));
    }

    public void SyncPreviewFrame(long elapsedMilliseconds)
    {
        if (!_previewState.SyncPreviewFrame(elapsedMilliseconds))
            return;

        CurrentPreviewBitmap = _previewState.CurrentBitmap;
        OnPropertyChanged(nameof(CurrentPreviewBitmap));
        OnPropertyChanged(nameof(PreviewDebugInfo));
    }

    public void EnableMemoryPlayback()
    {
        if (!_previewState.EnableMemoryPlayback())
            return;
        OnPropertyChanged(nameof(IsMemoryPreview));
    }

    public void DisableMemoryPlayback()
    {
        _previewState.DisableMemoryPlayback();
        OnPropertyChanged(nameof(IsMemoryPreview));
    }

    public void EnableSmoothPlayback()
    {
        if (!_previewState.EnableSmoothPlayback())
            return;

        CurrentPreviewBitmap = _previewState.CurrentBitmap;
        OnPropertyChanged(nameof(CurrentPreviewBitmap));
        OnPropertyChanged(nameof(IsSmoothPlaybackEnabled));
    }

    public void DisableSmoothPlayback()
    {
        if (!_previewState.DisableSmoothPlayback())
            return;

        CurrentPreviewBitmap = _previewState.CurrentBitmap;
        OnPropertyChanged(nameof(CurrentPreviewBitmap));
        OnPropertyChanged(nameof(IsSmoothPlaybackEnabled));
    }

    private void NotifyPreviewLifecycleStateChanged()
    {
        OnPropertyChanged(nameof(HasPreviewBitmap));
        OnPropertyChanged(nameof(NoPreviewBitmap));
        OnPropertyChanged(nameof(PreviewFrameCount));
        OnPropertyChanged(nameof(HasLoadedPreview));
        OnPropertyChanged(nameof(IsPreviewAnimated));
        OnPropertyChanged(nameof(IsLegacyPreview));
        OnPropertyChanged(nameof(IsMemoryPreview));
        OnPropertyChanged(nameof(IsSmoothPlaybackEnabled));
        OnPropertyChanged(nameof(PreviewDebugInfo));
    }
}
