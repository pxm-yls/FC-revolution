using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views.MainWindowParts;

public partial class MainWindowShelfView : MainWindowHostedControlBase
{
    private MainWindowViewModel? _lastVm;
    private readonly DispatcherTimer _shelfScrollIdleTimer;

    public MainWindowShelfView()
    {
        InitializeComponent();

        _shelfScrollIdleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _shelfScrollIdleTimer.Tick += (_, _) =>
        {
            _shelfScrollIdleTimer.Stop();
            SyncShelfViewportState(isScrolling: false);
        };

        ShelfScrollViewer.ScrollChanged += OnShelfScrollChanged;
        ShelfScrollBar.PropertyChanged += OnShelfScrollBarPropertyChanged;
        SizeChanged += (_, _) => UpdateShelfLayoutState();
        AttachedToVisualTree += (_, _) => UpdateShelfLayoutState();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_lastVm != null)
            _lastVm.PropertyChanged -= OnVmPropertyChanged;

        _lastVm = ViewModel;
        if (_lastVm != null)
            _lastVm.PropertyChanged += OnVmPropertyChanged;

        UpdateShelfLayoutState();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.LayoutMode) or nameof(MainWindowViewModel.ShelfSlots))
            UpdateShelfLayoutState();
    }

    private void UpdateShelfLayoutState()
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateShelfScrollMetrics();
            SyncShelfViewportState(isScrolling: false);
        }, DispatcherPriority.Loaded);
    }

    private void OnShelfScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateShelfScrollMetrics();
        SyncShelfViewportState(isScrolling: true);
        _shelfScrollIdleTimer.Stop();
        _shelfScrollIdleTimer.Start();
        if (Math.Abs(ShelfScrollBar.Value - ShelfScrollViewer.Offset.Y) > 0.5)
            ShelfScrollBar.Value = ShelfScrollViewer.Offset.Y;
    }

    private void OnShelfScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty)
            return;

        var targetOffset = new Vector(ShelfScrollViewer.Offset.X, ShelfScrollBar.Value);
        if (Math.Abs(ShelfScrollViewer.Offset.Y - targetOffset.Y) > 0.5)
            ShelfScrollViewer.Offset = targetOffset;
    }

    private void UpdateShelfScrollMetrics()
    {
        var extent = ShelfScrollViewer.Extent;
        var viewport = ShelfScrollViewer.Viewport;
        var scrollableHeight = Math.Max(0, extent.Height - viewport.Height);
        ShelfScrollBar.Maximum = scrollableHeight;
        ShelfScrollBar.ViewportSize = Math.Max(1, viewport.Height);
        ShelfScrollBar.IsVisible = scrollableHeight > 0;
    }

    private void SyncShelfViewportState(bool isScrolling)
    {
        if (ViewModel == null)
            return;

        var viewportHeight = ShelfScrollViewer.Viewport.Height > 0 && ShelfScrollViewer.Bounds.Height > 0
            ? Math.Min(ShelfScrollViewer.Viewport.Height, ShelfScrollViewer.Bounds.Height)
            : Math.Max(ShelfScrollViewer.Viewport.Height, ShelfScrollViewer.Bounds.Height);
        ViewModel.UpdateShelfViewport(ShelfScrollViewer.Offset.Y, viewportHeight, isScrolling);
    }

    private void OnShelfSlotTapped(object? sender, TappedEventArgs e)
    {
        if (TryGetRomFromControl(sender as Control, out var rom) && ViewModel != null)
        {
            ViewModel.PreviewRomFromShelf(rom);
            PositionBranchGalleryPreviewAbove(sender as Control, 220);
            e.Handled = true;
        }
    }

    private void OnShelfSlotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TryGetRomFromControl(sender as Control, out var rom) && ViewModel != null)
        {
            ViewModel.PlayRomFromShelf(rom);
            e.Handled = true;
        }
    }
}
