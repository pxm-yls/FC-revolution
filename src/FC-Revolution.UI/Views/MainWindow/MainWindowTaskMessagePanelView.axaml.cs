using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views.MainWindowParts;

public partial class MainWindowTaskMessagePanelView : MainWindowHostedControlBase
{
    private MainWindowViewModel? _lastVm;
    private readonly DispatcherTimer _taskMessagePanelAutoHideTimer;
    private bool _isPointerOver;

    public MainWindowTaskMessagePanelView()
    {
        InitializeComponent();

        _taskMessagePanelAutoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _taskMessagePanelAutoHideTimer.Tick += (_, _) =>
        {
            _taskMessagePanelAutoHideTimer.Stop();
            if (_isPointerOver || ViewModel is not MainWindowViewModel vm)
                return;

            vm.HideTaskMessagePanel();
        };

        TaskMessageScrollViewer.ScrollChanged += OnTaskMessageScrollChanged;
        TaskMessageScrollBar.PropertyChanged += OnTaskMessageScrollBarPropertyChanged;
        SizeChanged += (_, _) => UpdateTaskMessageScrollMetrics();
        AttachedToVisualTree += (_, _) => UpdateTaskMessageScrollMetrics();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();
    }

    public void ShowPanel()
    {
        _taskMessagePanelAutoHideTimer.Stop();
        ViewModel?.ShowTaskMessagePanel();
        ScrollTaskMessagePanelToBottom();
    }

    public void StartAutoHideCountdown()
    {
        _taskMessagePanelAutoHideTimer.Stop();
        if (_isPointerOver || ViewModel is not MainWindowViewModel { IsTaskMessagePanelVisible: true })
            return;

        _taskMessagePanelAutoHideTimer.Start();
    }

    private void AttachViewModel()
    {
        if (_lastVm != null)
        {
            _lastVm.PropertyChanged -= OnVmPropertyChanged;
            _lastVm.FilteredTaskMessages.CollectionChanged -= OnTaskMessageCollectionChanged;
        }

        _lastVm = ViewModel;
        if (_lastVm != null)
        {
            _lastVm.PropertyChanged += OnVmPropertyChanged;
            _lastVm.FilteredTaskMessages.CollectionChanged += OnTaskMessageCollectionChanged;
        }

        UpdateTaskMessageScrollMetrics();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsTaskMessagePanelVisible))
        {
            if (ViewModel is MainWindowViewModel { IsTaskMessagePanelVisible: true })
                ScrollTaskMessagePanelToBottom();
            else
                _taskMessagePanelAutoHideTimer.Stop();
        }
    }

    private void OnTaskMessageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTaskMessageScrollMetrics();
        if (ViewModel is MainWindowViewModel { IsTaskMessagePanelVisible: true })
            ScrollTaskMessagePanelToBottom();
    }

    private void OnTaskMessagePanelPointerEntered(object? sender, PointerEventArgs e)
    {
        _isPointerOver = true;
        _taskMessagePanelAutoHideTimer.Stop();
    }

    private void OnTaskMessagePanelPointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerOver = false;
        StartAutoHideCountdown();
    }

    private void OnTaskMessageScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateTaskMessageScrollMetrics();
        if (Math.Abs(TaskMessageScrollBar.Value - TaskMessageScrollViewer.Offset.Y) > 0.5)
            TaskMessageScrollBar.Value = TaskMessageScrollViewer.Offset.Y;
    }

    private void OnTaskMessageScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty)
            return;

        var targetOffset = new Vector(TaskMessageScrollViewer.Offset.X, TaskMessageScrollBar.Value);
        if (Math.Abs(TaskMessageScrollViewer.Offset.Y - targetOffset.Y) > 0.5)
            TaskMessageScrollViewer.Offset = targetOffset;
    }

    private void UpdateTaskMessageScrollMetrics()
    {
        var extent = TaskMessageScrollViewer.Extent;
        var viewport = TaskMessageScrollViewer.Viewport;
        var scrollableHeight = Math.Max(0, extent.Height - viewport.Height);
        TaskMessageScrollBar.Maximum = scrollableHeight;
        TaskMessageScrollBar.ViewportSize = Math.Max(1, viewport.Height);
        TaskMessageScrollBar.IsVisible = scrollableHeight > 0;
    }

    private void ScrollTaskMessagePanelToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateTaskMessageScrollMetrics();
            var offsetY = Math.Max(0, TaskMessageScrollViewer.Extent.Height - TaskMessageScrollViewer.Viewport.Height);
            TaskMessageScrollViewer.Offset = new Vector(TaskMessageScrollViewer.Offset.X, offsetY);
            TaskMessageScrollBar.Value = offsetY;
        }, DispatcherPriority.Background);
    }
}
