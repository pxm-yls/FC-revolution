using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowActiveInputWorkflowControllerTests
{
    [Fact]
    public void GetActiveInputRomPath_WhenLoaded_ReturnsLoadedRomPath()
    {
        var controller = new MainWindowActiveInputWorkflowController();
        var selectedRom = new RomLibraryItem("contra", "/tmp/selected.nes", "", false, 0, DateTime.UtcNow);

        var path = controller.GetActiveInputRomPath(
            isRomLoaded: true,
            loadedRomPath: "/tmp/loaded.nes",
            currentRom: selectedRom);

        Assert.Equal("/tmp/loaded.nes", path);
    }

    [Fact]
    public void BuildRefreshDecision_WhenRomNotLoaded_ResetsRuntimeAndIgnoresBindings()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var decision = workflowController.BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded: false,
            activeRomPath: null,
            effectiveKeyMap: new Dictionary<Key, (string PortId, string ActionId)>
            {
                [Key.Z] = ("p1", FallbackInputTestData.ActionA)
            },
            effectiveExtraBindings:
            [
                new ResolvedExtraInputBinding("p1", Key.Q, ExtraInputBindingKind.Turbo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA))
            ],
            inputBindingSchema);

        Assert.Empty(decision.ApplyPlan.WriteRequests);
        Assert.False(decision.LegacyMirror.TurboPulseActive);
        Assert.Empty(decision.LegacyMirror.PressedKeys);
    }

    [Fact]
    public void ApplyRefreshDecision_WritesInputAndReturnsMirror()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var decision = workflowController.BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded: true,
            activeRomPath: "/tmp/demo.nes",
            effectiveKeyMap: new Dictionary<Key, (string PortId, string ActionId)>
            {
                [Key.Z] = ("p1", FallbackInputTestData.ActionA)
            },
            effectiveExtraBindings: [],
            inputBindingSchema);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(string PortId, string ActionId, bool Pressed)>();
        var mirror = workflowController.ApplyRefreshDecision(
            runtimeController,
            decision,
            new object(),
            writer,
            (portId, actionId, pressed) => maskUpdates.Add((portId, actionId, pressed)));

        var call = Assert.Single(writer.Calls);
        Assert.Equal(("p1", "a", 1f), call);
        Assert.Equal([("p1", "a", true)], maskUpdates);
        Assert.Equal([Key.Z], mirror.PressedKeys.ToArray());
        Assert.False(mirror.TurboPulseActive);
    }

    [Fact]
    public void RefreshActiveInputState_CombinesBuildAndApplyAndReturnsMirrors()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(string PortId, string ActionId, bool Pressed)>();
        var result = workflowController.RefreshActiveInputState(
            runtimeController,
            inputStateController,
            isRomLoaded: true,
            activeRomPath: "/tmp/demo.nes",
            effectiveKeyMap: new Dictionary<Key, (string PortId, string ActionId)>
            {
                [Key.Z] = ("p1", FallbackInputTestData.ActionA)
            },
            effectiveExtraBindings: [],
            inputBindingSchema,
            new object(),
            writer,
            (portId, actionId, pressed) => maskUpdates.Add((portId, actionId, pressed)));

        Assert.Equal([Key.Z], result.LegacyMirrorBeforeApply.PressedKeys.ToArray());
        Assert.Equal([Key.Z], result.LegacyMirrorAfterApply.PressedKeys.ToArray());
        Assert.False(result.LegacyMirrorBeforeApply.TurboPulseActive);
        Assert.False(result.LegacyMirrorAfterApply.TurboPulseActive);

        var call = Assert.Single(writer.Calls);
        Assert.Equal(("p1", "a", 1f), call);
        Assert.Equal([("p1", "a", true)], maskUpdates);
    }

    [Fact]
    public void UpdateTurboPulse_ReturnsRefreshFlagAndMirror()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Q);

        var decision = workflowController.UpdateTurboPulse(
            runtimeController,
            inputStateController,
            [new ResolvedExtraInputBinding("p1", Key.Q, ExtraInputBindingKind.Turbo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA))]);

        Assert.True(decision.ShouldRefreshActiveInputState);
        Assert.True(decision.LegacyMirror.TurboPulseActive);
        Assert.Equal([Key.Q], decision.LegacyMirror.PressedKeys.ToArray());
    }

    private sealed class FakeInputStateWriter : ICoreInputStateWriter
    {
        public List<(string PortId, string ActionId, float Value)> Calls { get; } = [];

        public void SetInputState(string portId, string actionId, float value)
        {
            Calls.Add((portId, actionId, value));
        }
    }
}
