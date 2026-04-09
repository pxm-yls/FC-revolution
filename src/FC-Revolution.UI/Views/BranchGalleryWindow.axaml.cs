using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

public partial class BranchGalleryWindow : Window
{
    private bool _isPanning;
    private Point _lastPanPoint;

    public BranchGalleryWindow()
    {
        InitializeComponent();
    }

    private void OnCanvasWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not BranchGalleryViewModel vm)
            return;

        vm.AdjustZoom(e.Delta.Y > 0 ? 0.12 : -0.12);
        e.Handled = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            return;

        _isPanning = true;
        _lastPanPoint = e.GetPosition(CanvasScrollViewer);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
            return;

        var current = e.GetPosition(CanvasScrollViewer);
        var delta = current - _lastPanPoint;
        CanvasScrollViewer.Offset = new Vector(
            Math.Max(0, CanvasScrollViewer.Offset.X - delta.X),
            Math.Max(0, CanvasScrollViewer.Offset.Y - delta.Y));
        _lastPanPoint = current;
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
    }

    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is BranchGalleryViewModel vm && vm.HasBranchSelection)
            vm.LoadBranchCommand.Execute(null);

        e.Handled = true;
    }
}
