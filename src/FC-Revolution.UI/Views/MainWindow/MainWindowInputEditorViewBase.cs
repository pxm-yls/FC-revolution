using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Views.MainWindowParts;

public abstract class MainWindowInputEditorViewBase : MainWindowHostedControlBase
{
    private InputBindingEntry? _draggingLayoutEntry;
    private string? _draggingDecorationId;
    private Point _lastLayoutDragPoint;

    protected void OnSettingsBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel != null)
        {
            if (ViewModel.IsQuickRomInputEditorOpen)
                ViewModel.CloseQuickRomInputEditorCommand.Execute(null);
            else
                ViewModel.CloseSettingsCommand.Execute(null);
        }

        e.Handled = true;
    }

    protected void OnSettingsCardPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    protected void OnInputBindingBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is InputElement element)
            element.Focus();
    }

    protected void OnInputBindingBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is Control { DataContext: IKeyCaptureBinding binding })
            binding.IsCapturing = true;
    }

    protected void OnInputBindingBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: IKeyCaptureBinding binding })
            binding.IsCapturing = false;
    }

    protected void OnInputBindingBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: IKeyCaptureBinding binding })
            return;

        if (e.Key == Key.Tab)
            return;

        if (e.Key == Key.Escape)
        {
            binding.IsCapturing = false;
            e.Handled = true;
            return;
        }

        if (!binding.TrySetSelectedKey(e.Key))
            return;

        e.Handled = true;
    }

    protected void OnShortcutBindingBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is Control { DataContext: ShortcutBindingEntry binding })
            binding.BeginCapture();
    }

    protected void OnShortcutBindingBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ShortcutBindingEntry binding })
            binding.CancelCapture();
    }

    protected void OnShortcutBindingBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: ShortcutBindingEntry binding } ||
            ViewModel == null)
        {
            return;
        }

        if (e.Key == Key.Tab)
            return;

        if (e.Key == Key.Escape)
        {
            binding.CancelCapture();
            e.Handled = true;
            return;
        }

        if (!ViewModel.TryCommitShortcutBinding(binding, e.Key, e.KeyModifiers))
            return;

        e.Handled = true;
    }

    protected void OnDebugInputBindingPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: InputBindingEntry entry } control)
            return;

        var surface = this.FindControl<Grid>("InputLayoutDebugSurface");
        if (surface == null)
            return;

        _draggingLayoutEntry = entry;
        _lastLayoutDragPoint = e.GetPosition(surface);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    protected void OnDebugInputBindingPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingLayoutEntry == null ||
            sender is not Control control ||
            !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed ||
            ViewModel == null)
        {
            return;
        }

        var surface = this.FindControl<Grid>("InputLayoutDebugSurface");
        if (surface == null)
            return;

        var currentPoint = e.GetPosition(surface);
        var delta = currentPoint - _lastLayoutDragPoint;
        if (Math.Abs(delta.X) < double.Epsilon && Math.Abs(delta.Y) < double.Epsilon)
            return;

        _lastLayoutDragPoint = currentPoint;
        ViewModel.MoveInputBindingLayoutSlot(_draggingLayoutEntry.Button, delta.X, delta.Y);
        e.Handled = true;
    }

    protected void OnDebugInputBindingPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingLayoutEntry == null)
            return;

        if (sender is InputElement)
            e.Pointer.Capture(null);

        ViewModel?.SaveInputBindingLayout();
        _draggingLayoutEntry = null;
        e.Handled = true;
    }

    protected void OnDebugDecorationPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string decorationId } control)
            return;

        var surface = this.FindControl<Grid>("InputLayoutDebugSurface");
        if (surface == null)
            return;

        _draggingDecorationId = decorationId;
        _lastLayoutDragPoint = e.GetPosition(surface);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    protected void OnDebugDecorationPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingDecorationId == null ||
            sender is not Control control ||
            !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed ||
            ViewModel == null)
        {
            return;
        }

        var surface = this.FindControl<Grid>("InputLayoutDebugSurface");
        if (surface == null)
            return;

        var currentPoint = e.GetPosition(surface);
        var delta = currentPoint - _lastLayoutDragPoint;
        if (Math.Abs(delta.X) < double.Epsilon && Math.Abs(delta.Y) < double.Epsilon)
            return;

        _lastLayoutDragPoint = currentPoint;
        ViewModel.MoveInputLayoutDecoration(_draggingDecorationId, delta.X, delta.Y);
        e.Handled = true;
    }

    protected void OnDebugDecorationPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingDecorationId == null)
            return;

        if (sender is InputElement)
            e.Pointer.Capture(null);

        ViewModel?.SaveInputBindingLayout();
        _draggingDecorationId = null;
        e.Handled = true;
    }
}
