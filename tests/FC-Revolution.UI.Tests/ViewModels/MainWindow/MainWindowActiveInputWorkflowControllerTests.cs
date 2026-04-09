using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;
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
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var decision = workflowController.BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded: false,
            activeRomPath: null,
            effectiveKeyMap: new Dictionary<Key, (int Player, NesButton Button)>
            {
                [Key.Z] = (0, NesButton.A)
            },
            effectiveExtraBindings:
            [
                new ResolvedExtraInputBinding(0, Key.Q, ExtraInputBindingKind.Turbo, [NesButton.A])
            ],
            player1CurrentMask: (byte)NesButton.A,
            player2CurrentMask: 0,
            ControllerButtons,
            GetInputPortId,
            GetInputActionId);

        var write = Assert.Single(decision.ApplyPlan.WriteRequests);
        Assert.Equal(0, write.Player);
        Assert.Equal(NesButton.A, write.Button);
        Assert.False(write.DesiredPressed);
        Assert.False(decision.LegacyMirror.TurboPulseActive);
        Assert.Empty(decision.LegacyMirror.PressedKeys);
    }

    [Fact]
    public void ApplyRefreshDecision_WritesInputAndReturnsMirror()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var decision = workflowController.BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded: true,
            activeRomPath: "/tmp/demo.nes",
            effectiveKeyMap: new Dictionary<Key, (int Player, NesButton Button)>
            {
                [Key.Z] = (0, NesButton.A)
            },
            effectiveExtraBindings: [],
            player1CurrentMask: 0,
            player2CurrentMask: 0,
            ControllerButtons,
            GetInputPortId,
            GetInputActionId);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(int Player, NesButton Button, bool Pressed)>();
        var mirror = workflowController.ApplyRefreshDecision(
            runtimeController,
            decision,
            new object(),
            writer,
            (player, button, pressed) => maskUpdates.Add((player, button, pressed)));

        var call = Assert.Single(writer.Calls);
        Assert.Equal(("p1", "a", 1f), call);
        Assert.Equal([(0, NesButton.A, true)], maskUpdates);
        Assert.Equal([Key.Z], mirror.PressedKeys.ToArray());
        Assert.False(mirror.TurboPulseActive);
    }

    [Fact]
    public void RefreshActiveInputState_CombinesBuildAndApplyAndReturnsMirrors()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var workflowController = new MainWindowActiveInputWorkflowController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/demo.nes");
        runtimeController.PressKey(Key.Z);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(int Player, NesButton Button, bool Pressed)>();
        var result = workflowController.RefreshActiveInputState(
            runtimeController,
            inputStateController,
            isRomLoaded: true,
            activeRomPath: "/tmp/demo.nes",
            effectiveKeyMap: new Dictionary<Key, (int Player, NesButton Button)>
            {
                [Key.Z] = (0, NesButton.A)
            },
            effectiveExtraBindings: [],
            player1CurrentMask: 0,
            player2CurrentMask: 0,
            ControllerButtons,
            GetInputPortId,
            GetInputActionId,
            new object(),
            writer,
            (player, button, pressed) => maskUpdates.Add((player, button, pressed)));

        Assert.Equal([Key.Z], result.LegacyMirrorBeforeApply.PressedKeys.ToArray());
        Assert.Equal([Key.Z], result.LegacyMirrorAfterApply.PressedKeys.ToArray());
        Assert.False(result.LegacyMirrorBeforeApply.TurboPulseActive);
        Assert.False(result.LegacyMirrorAfterApply.TurboPulseActive);

        var call = Assert.Single(writer.Calls);
        Assert.Equal(("p1", "a", 1f), call);
        Assert.Equal([(0, NesButton.A, true)], maskUpdates);
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
            [new ResolvedExtraInputBinding(0, Key.Q, ExtraInputBindingKind.Turbo, [NesButton.A])]);

        Assert.True(decision.ShouldRefreshActiveInputState);
        Assert.True(decision.LegacyMirror.TurboPulseActive);
        Assert.Equal([Key.Q], decision.LegacyMirror.PressedKeys.ToArray());
    }

    private static IReadOnlyList<NesButton> ControllerButtons =>
    [
        NesButton.A,
        NesButton.B,
        NesButton.Select,
        NesButton.Start,
        NesButton.Up,
        NesButton.Down,
        NesButton.Left,
        NesButton.Right
    ];

    private static string GetInputPortId(int player) => player == 0 ? "p1" : "p2";

    private static string GetInputActionId(NesButton button) => button switch
    {
        NesButton.A => "a",
        NesButton.B => "b",
        NesButton.Select => "select",
        NesButton.Start => "start",
        NesButton.Up => "up",
        NesButton.Down => "down",
        NesButton.Left => "left",
        NesButton.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
    };

    private sealed class FakeInputStateWriter : ICoreInputStateWriter
    {
        public List<(string PortId, string ActionId, float Value)> Calls { get; } = [];

        public void SetInputState(string portId, string actionId, float value)
        {
            Calls.Add((portId, actionId, value));
        }
    }
}
