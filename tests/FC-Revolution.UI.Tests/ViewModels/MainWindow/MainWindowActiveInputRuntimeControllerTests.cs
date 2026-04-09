using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowActiveInputRuntimeControllerTests
{
    [Fact]
    public void BuildApplyPlan_AndApplyPlan_ProducesExpectedWriteRequest()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/test.nes");
        runtimeController.PressKey(Key.Z);

        var plan = runtimeController.BuildApplyPlan(
            inputStateController,
            new Dictionary<Key, (int Player, string ActionId)>
            {
                [Key.Z] = (0, NesInputTestAdapter.ActionId(NesButton.A))
            },
            [],
            player1CurrentMask: 0,
            player2CurrentMask: 0,
            ControllerActionIds,
            GetInputPortId);

        Assert.Equal((byte)NesButton.A, plan.DesiredPlayer1Mask);
        Assert.Equal((byte)0, plan.DesiredPlayer2Mask);
        var write = Assert.Single(plan.WriteRequests);
        Assert.Equal(0, write.Player);
        Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), write.ActionId);
        Assert.True(write.DesiredPressed);
        Assert.Equal("p1", write.PortId);
        Assert.Equal("a", write.ActionId);
        Assert.Equal(1f, write.Value);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(int Player, string ActionId, bool Pressed)>();
        runtimeController.ApplyPlan(
            plan,
            new object(),
            writer,
            (player, actionId, pressed) => maskUpdates.Add((player, actionId, pressed)));

        var call = Assert.Single(writer.Calls);
        Assert.Equal("p1", call.PortId);
        Assert.Equal("a", call.ActionId);
        Assert.Equal(1f, call.Value);
        Assert.Equal([(0, "a", true)], maskUpdates);
    }

    [Fact]
    public void BuildApplyPlan_WhenDesiredMaskIsZero_ReleasesCurrentMaskButtons()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/test.nes");

        var plan = runtimeController.BuildApplyPlan(
            inputStateController,
            new Dictionary<Key, (int Player, string ActionId)>(),
            [],
            player1CurrentMask: (byte)NesButton.A,
            player2CurrentMask: 0,
            ControllerActionIds,
            GetInputPortId);

        var write = Assert.Single(plan.WriteRequests);
        Assert.Equal(0, write.Player);
        Assert.Equal("a", write.ActionId);
        Assert.False(write.DesiredPressed);
        Assert.Equal(0f, write.Value);
    }

    [Fact]
    public void RefreshContext_WhenRomChanges_ResetsPressedKeysAndTurboPulse()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var inputStateController = new MainWindowInputStateController();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/test.nes");
        runtimeController.PressKey(Key.Q);
        runtimeController.UpdateTurboPulse(
            inputStateController,
            [new ResolvedExtraInputBinding(0, Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A))]);

        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/another.nes");

        Assert.False(runtimeController.TurboPulseActive);
        Assert.Empty(runtimeController.PressedKeys);
    }

    private static IReadOnlyList<string> ControllerActionIds => NesInputTestAdapter.ActionIds(
        NesButton.A,
        NesButton.B,
        NesButton.Select,
        NesButton.Start,
        NesButton.Up,
        NesButton.Down,
        NesButton.Left,
        NesButton.Right);

    private static string GetInputPortId(int player) => player == 0 ? "p1" : "p2";

    private sealed class FakeInputStateWriter : ICoreInputStateWriter
    {
        public List<(string PortId, string ActionId, float Value)> Calls { get; } = [];

        public void SetInputState(string portId, string actionId, float value)
        {
            Calls.Add((portId, actionId, value));
        }
    }
}
