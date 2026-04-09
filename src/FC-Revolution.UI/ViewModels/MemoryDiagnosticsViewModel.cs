using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public sealed class MemoryDiagnosticsViewModel : ViewModelBase, IDisposable
{
    private const int MaxHistoryEntries = 240;

    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly DispatcherTimer _timer;
    private string _windowSummary = "等待采样";
    private string _processSummary = "等待采样";
    private string _gcSummary = "等待采样";
    private string _previewSummary = "等待采样";
    private string _cacheSummary = "等待采样";
    private string _currentRomSummary = "当前游戏: 无";
    private bool _disposed;

    public MemoryDiagnosticsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        PreviewItems = new ObservableCollection<PreviewMemoryUsageItem>();
        HistoryItems = new ObservableCollection<MemoryDiagnosticsSampleItem>();
        CaptureNowCommand = new RelayCommand(CaptureNow);
        ClearHistoryCommand = new RelayCommand(ClearHistory);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => CaptureNow();
        _timer.Start();

        CaptureNow();
    }

    public string WindowSummary
    {
        get => _windowSummary;
        private set => SetProperty(ref _windowSummary, value);
    }

    public string ProcessSummary
    {
        get => _processSummary;
        private set => SetProperty(ref _processSummary, value);
    }

    public string GcSummary
    {
        get => _gcSummary;
        private set => SetProperty(ref _gcSummary, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public string CacheSummary
    {
        get => _cacheSummary;
        private set => SetProperty(ref _cacheSummary, value);
    }

    public string CurrentRomSummary
    {
        get => _currentRomSummary;
        private set => SetProperty(ref _currentRomSummary, value);
    }

    public ObservableCollection<PreviewMemoryUsageItem> PreviewItems { get; }

    public ObservableCollection<MemoryDiagnosticsSampleItem> HistoryItems { get; }

    public IRelayCommand CaptureNowCommand { get; }

    public IRelayCommand ClearHistoryCommand { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
    }

    private void CaptureNow()
    {
        if (_disposed)
            return;

        var snapshot = _mainWindowViewModel.CaptureMemoryDiagnosticsSnapshot();
        UpdateSummary(snapshot);
        ReplacePreviewItems(snapshot);
        AppendHistory(snapshot);
    }

    private void ClearHistory()
    {
        HistoryItems.Clear();
        WindowSummary = "历史已清空，继续采样中";
    }

    private void UpdateSummary(MemoryDiagnosticsSnapshot snapshot)
    {
        WindowSummary = $"最近采样 {snapshot.TimestampLocal:HH:mm:ss} | 布局: {snapshot.LayoutName} | ROM: {snapshot.TotalRomCount}";
        ProcessSummary = $"工作集 {FormatBytes(snapshot.WorkingSetBytes)} | 私有 {FormatBytes(snapshot.PrivateBytes)} | 虚拟 {FormatBytes(snapshot.VirtualBytes)}";
        GcSummary = $"托管 {FormatBytes(snapshot.ManagedHeapBytes)} | 堆大小 {FormatBytes(snapshot.HeapSizeBytes)} | 已提交 {FormatBytes(snapshot.TotalCommittedBytes)} | 碎片 {FormatBytes(snapshot.FragmentedBytes)} | GC {snapshot.Gen0Collections}/{snapshot.Gen1Collections}/{snapshot.Gen2Collections}";
        PreviewSummary = $"已载入 {snapshot.LoadedPreviewCount} | 动画 {snapshot.AnimatedPreviewCount} | 完整帧 {snapshot.SmoothPlaybackCount} | 流句柄 {snapshot.StreamHandleCount} | 预读 {snapshot.PrefetchedFrameCount} | 当前页目标 {snapshot.VisiblePreviewTargetCount}";
        CacheSummary = $"位图缓存 {snapshot.TotalCachedBitmapCount} 帧 / {FormatBytes(snapshot.EstimatedBitmapCacheBytes)} | 原始帧缓存 {snapshot.TotalCachedFrameCount} 帧 / {FormatBytes(snapshot.EstimatedFrameCacheBytes)} | 内存回放 {snapshot.MemoryBackedPreviewCount}";
        CurrentRomSummary = snapshot.CurrentRomName is { Length: > 0 }
            ? $"当前游戏: {snapshot.CurrentRomName}"
            : "当前游戏: 无";
    }

    private void ReplacePreviewItems(MemoryDiagnosticsSnapshot snapshot)
    {
        PreviewItems.Clear();
        foreach (var item in snapshot.PreviewItems)
            PreviewItems.Add(item);
    }

    private void AppendHistory(MemoryDiagnosticsSnapshot snapshot)
    {
        HistoryItems.Insert(0, new MemoryDiagnosticsSampleItem
        {
            TimeLabel = snapshot.TimestampLocal.ToString("HH:mm:ss"),
            WorkingSetLabel = FormatBytes(snapshot.WorkingSetBytes),
            PrivateLabel = FormatBytes(snapshot.PrivateBytes),
            ManagedLabel = FormatBytes(snapshot.ManagedHeapBytes),
            PreviewLabel = $"载入 {snapshot.LoadedPreviewCount} / 完整帧 {snapshot.SmoothPlaybackCount}",
            CacheLabel = $"位图 {FormatBytes(snapshot.EstimatedBitmapCacheBytes)} | 原始 {FormatBytes(snapshot.EstimatedFrameCacheBytes)}",
            LayoutLabel = $"{snapshot.LayoutName} | 当前页 {snapshot.VisiblePreviewTargetCount}"
        });

        while (HistoryItems.Count > MaxHistoryEntries)
            HistoryItems.RemoveAt(HistoryItems.Count - 1);
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;

        return bytes switch
        {
            >= (long)gb => $"{bytes / gb:F2} GB",
            >= (long)mb => $"{bytes / mb:F1} MB",
            >= (long)kb => $"{bytes / kb:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
