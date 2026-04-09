using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _lastVm;
    private readonly MainWindowBranchGalleryPreviewController _branchGalleryPreviewController;
    private readonly MainWindowKeyRouter _keyRouter;
    private readonly MainWindowMemoryDiagnosticsController _memoryDiagnosticsController;

    public MainWindow()
    {
        StartupDiagnostics.Write("window", "MainWindow ctor begin");
        InitializeComponent();
        StartupDiagnostics.Write("window", "InitializeComponent complete");
        _memoryDiagnosticsController = new MainWindowMemoryDiagnosticsController(this);
        _branchGalleryPreviewController = new MainWindowBranchGalleryPreviewController(
            this,
            () => DataContext as MainWindowViewModel,
            () => CarouselView.PreviewAnchor);
        _keyRouter = new MainWindowKeyRouter(
            this,
            () => DataContext as MainWindowViewModel,
            TaskMessagePanelView,
            _memoryDiagnosticsController);
        Opened += OnOpened;
        Closing += OnClosing;
        SizeChanged += OnWindowSizeChanged;
        // Tunnel = fires top-down before focused control (menu) can consume the event
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent,   OnKeyUp,   RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (_lastVm != null)
                _lastVm.PropertyChanged -= OnVmPropertyChanged;

            _lastVm = DataContext as MainWindowViewModel;
            if (_lastVm != null)
                _lastVm.PropertyChanged += OnVmPropertyChanged;
        };
        StartupDiagnostics.Write("window", "MainWindow ctor complete");
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsRomLoaded) &&
            DataContext is MainWindowViewModel { IsRomLoaded: true })
        {
            Focus(); // reclaim focus from menu after ROM is loaded
        }

        _branchGalleryPreviewController.HandleViewModelPropertyChanged(e.PropertyName);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keyRouter.HandleKeyDown(e);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keyRouter.HandleKeyUp(e);
    }

    private void OnBranchGalleryPreviewCardPressed(object? sender, PointerPressedEventArgs e)
    {
        _branchGalleryPreviewController.HandlePreviewCardPressed(e);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        StartupDiagnostics.Write("window", "Opened event begin");
        if (DataContext is MainWindowViewModel vm)
        {
            StartupDiagnostics.Write("window", "calling MainWindowViewModel.OnHostWindowOpenedAsync");
            await vm.OnHostWindowOpenedAsync();
            StartupDiagnostics.Write("window", "MainWindowViewModel.OnHostWindowOpenedAsync returned");
        }

        var screen = Screens.ScreenFromWindow(this);
        if (screen == null)
        {
            StartupDiagnostics.Write("window", "no screen found for MainWindow");
            return;
        }

        var workingArea = screen.WorkingArea;
        var targetWidth = Math.Min(Width, workingArea.Width * 0.94);
        var targetHeight = Math.Min(Height, workingArea.Height * 0.92);

        MinWidth = Math.Min(MinWidth, targetWidth);
        MinHeight = Math.Min(MinHeight, targetHeight);
        Width = targetWidth;
        Height = targetHeight;
        _branchGalleryPreviewController.UpdateAnchor();
        StartupDiagnostics.Write("window", "Opened event complete");
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        _memoryDiagnosticsController.Close();

        if (vm.IsShuttingDown)
            return;

        e.Cancel = true;
        vm.ExitCommand.Execute(null);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _branchGalleryPreviewController.UpdateAnchor();
    }
}
