using System;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputLayoutControllerTests
{
    private static readonly IReadOnlyList<Key> ConfigurableKeys = [Key.Z, Key.X, Key.A, Key.S];

    [Fact]
    public void MoveInputBindingLayoutSlot_SanitizesAndReappliesLayoutToEntries()
    {
        var controller = new MainWindowInputLayoutController();
        var layout = InputBindingLayoutProfile.CreateDefault();
        layout.A.CenterX = 355;
        layout.A.CenterY = 149;
        var globalEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.A) };
        var romEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.A) };
        var notified = new List<string>();

        controller.MoveInputBindingLayoutSlot(
            layout,
            NesInputTestAdapter.ActionId(NesButton.A),
            deltaX: 20,
            deltaY: 20,
            globalEntries,
            romEntries,
            propertyName => notified.Add(propertyName));

        Assert.Equal(356, layout.A.CenterX);
        Assert.Equal(150, layout.A.CenterY);
        Assert.Equal(layout.A.CenterX, globalEntries[0].CenterX);
        Assert.Equal(layout.A.CenterY, globalEntries[0].CenterY);
        Assert.Equal(layout.A.CenterX, romEntries[0].CenterX);
        Assert.Equal(layout.A.CenterY, romEntries[0].CenterY);
        Assert.Contains(nameof(MainWindowViewModel.InputLayoutDebugSummary), notified);
        Assert.Contains(nameof(MainWindowViewModel.InputLayoutStartDecorationMargin), notified);
    }

    [Fact]
    public void MoveInputLayoutDecoration_UpdatesTargetField_UnknownIdNoOp()
    {
        var controller = new MainWindowInputLayoutController();
        var layout = InputBindingLayoutProfile.CreateDefault();
        var globalEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.A) };
        var romEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.B) };
        var notified = new List<string>();
        var beforeLeftCircleX = layout.LeftCircleX;

        controller.MoveInputLayoutDecoration(
            layout,
            decorationId: "Bridge",
            deltaX: 3,
            deltaY: -2,
            globalEntries,
            romEntries,
            propertyName => notified.Add(propertyName));

        Assert.Equal(InputBindingLayoutProfile.CreateDefault().BridgeX + 3, layout.BridgeX, precision: 6);
        Assert.Equal(InputBindingLayoutProfile.CreateDefault().BridgeY - 2, layout.BridgeY, precision: 6);
        Assert.Equal(beforeLeftCircleX, layout.LeftCircleX);
        var notifyCountAfterBridge = notified.Count;

        controller.MoveInputLayoutDecoration(
            layout,
            decorationId: "UnknownDecorationId",
            deltaX: 100,
            deltaY: 100,
            globalEntries,
            romEntries,
            propertyName => notified.Add(propertyName));

        Assert.Equal(notifyCountAfterBridge, notified.Count);
        Assert.Equal(beforeLeftCircleX, layout.LeftCircleX);
    }

    [Fact]
    public void ResetInputBindingLayout_RestoresDefaults_AppliesLayout_AndSaves()
    {
        var controller = new MainWindowInputLayoutController();
        var modifiedLayout = InputBindingLayoutProfile.CreateDefault();
        modifiedLayout.BridgeX = 10;
        modifiedLayout.BridgeY = 12;
        modifiedLayout.A.CenterX = 42;
        modifiedLayout.A.CenterY = 43;
        var globalEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.A) };
        var romEntries = new List<InputBindingEntry> { CreateInputBinding(NesButton.A) };
        var notified = new List<string>();
        var saveCalls = 0;
        var defaults = InputBindingLayoutProfile.CreateDefault();

        var resetLayout = controller.ResetInputBindingLayout(
            globalEntries,
            romEntries,
            propertyName => notified.Add(propertyName),
            () => saveCalls++);

        Assert.Equal(defaults.BridgeX, resetLayout.BridgeX, precision: 6);
        Assert.Equal(defaults.BridgeY, resetLayout.BridgeY, precision: 6);
        Assert.Equal(defaults.A.CenterX, resetLayout.A.CenterX, precision: 6);
        Assert.Equal(defaults.A.CenterY, resetLayout.A.CenterY, precision: 6);
        Assert.Equal(defaults.A.CenterX, globalEntries[0].CenterX, precision: 6);
        Assert.Equal(defaults.A.CenterY, romEntries[0].CenterY, precision: 6);
        Assert.Equal(1, saveCalls);
        Assert.Contains(nameof(MainWindowViewModel.InputLayoutDebugSummary), notified);
        Assert.Contains(nameof(MainWindowViewModel.InputLayoutBridgeMargin), notified);
    }

    private static InputBindingEntry CreateInputBinding(NesButton button) =>
        new(
            portId: "p1",
            portLabel: "1P",
            actionId: NesInputTestAdapter.ActionId(button),
            actionName: button.ToString(),
            selectedKey: Key.Z,
            availableKeys: ConfigurableKeys);
}
