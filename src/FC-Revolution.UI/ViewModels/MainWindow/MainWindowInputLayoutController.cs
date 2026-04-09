using System;
using System.Collections.Generic;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowInputLayoutController
{
    private static readonly string[] LayoutChangedPropertyNames =
    [
        nameof(MainWindowViewModel.InputLayoutDebugSummary),
        nameof(MainWindowViewModel.InputLayoutBridgeMargin),
        nameof(MainWindowViewModel.InputLayoutLeftCircleMargin),
        nameof(MainWindowViewModel.InputLayoutDPadHorizontalMargin),
        nameof(MainWindowViewModel.InputLayoutDPadVerticalMargin),
        nameof(MainWindowViewModel.InputLayoutRightCircleMargin),
        nameof(MainWindowViewModel.InputLayoutBDecorationMargin),
        nameof(MainWindowViewModel.InputLayoutADecorationMargin),
        nameof(MainWindowViewModel.InputLayoutSelectDecorationMargin),
        nameof(MainWindowViewModel.InputLayoutStartDecorationMargin)
    ];

    public void MoveInputBindingLayoutSlot(
        InputBindingLayoutProfile layout,
        NesButton button,
        double deltaX,
        double deltaY,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        Action<string> notifyPropertyChanged)
    {
        var slot = layout.GetSlot(button);
        slot.CenterX += deltaX;
        slot.CenterY += deltaY;
        layout.Sanitize();
        ApplyInputBindingLayoutToAllEntries(layout, globalInputBindings, romInputBindings, notifyPropertyChanged);
    }

    public void MoveInputLayoutDecoration(
        InputBindingLayoutProfile layout,
        string decorationId,
        double deltaX,
        double deltaY,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        Action<string> notifyPropertyChanged)
    {
        if (!TryMoveDecoration(layout, decorationId, deltaX, deltaY))
            return;

        layout.Sanitize();
        ApplyInputBindingLayoutToAllEntries(layout, globalInputBindings, romInputBindings, notifyPropertyChanged);
    }

    public void SaveInputBindingLayout(Action saveSystemConfig) => saveSystemConfig();

    public InputBindingLayoutProfile ResetInputBindingLayout(
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        Action<string> notifyPropertyChanged,
        Action saveSystemConfig)
    {
        var layout = InputBindingLayoutProfile.CreateDefault();
        ApplyInputBindingLayoutToAllEntries(layout, globalInputBindings, romInputBindings, notifyPropertyChanged);
        saveSystemConfig();
        return layout;
    }

    public void ApplyInputBindingLayoutToAllEntries(
        InputBindingLayoutProfile layout,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        Action<string> notifyPropertyChanged)
    {
        ApplyInputBindingLayout(layout, globalInputBindings);
        ApplyInputBindingLayout(layout, romInputBindings);
        foreach (var propertyName in LayoutChangedPropertyNames)
            notifyPropertyChanged(propertyName);
    }

    public void ApplyInputBindingLayout(InputBindingLayoutProfile layout, IEnumerable<InputBindingEntry> entries)
    {
        foreach (var entry in entries)
            entry.ApplyLayout(layout);
    }

    private static bool TryMoveDecoration(InputBindingLayoutProfile layout, string decorationId, double deltaX, double deltaY)
    {
        switch (decorationId)
        {
            case "Bridge":
                layout.BridgeX += deltaX;
                layout.BridgeY += deltaY;
                return true;
            case "LeftCircle":
                layout.LeftCircleX += deltaX;
                layout.LeftCircleY += deltaY;
                return true;
            case "DPadHorizontal":
                layout.DPadHorizontalX += deltaX;
                layout.DPadHorizontalY += deltaY;
                return true;
            case "DPadVertical":
                layout.DPadVerticalX += deltaX;
                layout.DPadVerticalY += deltaY;
                return true;
            case "RightCircle":
                layout.RightCircleX += deltaX;
                layout.RightCircleY += deltaY;
                return true;
            case "BDecoration":
                layout.BDecorationX += deltaX;
                layout.BDecorationY += deltaY;
                return true;
            case "ADecoration":
                layout.ADecorationX += deltaX;
                layout.ADecorationY += deltaY;
                return true;
            case "SelectDecoration":
                layout.SelectDecorationX += deltaX;
                layout.SelectDecorationY += deltaY;
                return true;
            case "StartDecoration":
                layout.StartDecorationX += deltaX;
                layout.StartDecorationY += deltaY;
                return true;
            default:
                return false;
        }
    }
}
