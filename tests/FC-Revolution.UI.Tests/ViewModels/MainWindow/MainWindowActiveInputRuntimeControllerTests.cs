using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
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
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/test.nes");
        runtimeController.PressKey(Key.Z);

        var plan = runtimeController.BuildApplyPlan(
            inputStateController,
            new Dictionary<Key, (string PortId, string ActionId)>
            {
                [Key.Z] = ("p1", NesInputTestAdapter.ActionId(NesButton.A))
            },
            [],
            inputBindingSchema);

        Assert.Equal((byte)NesButton.A, plan.DesiredLegacyMasksByPort["p1"]);
        Assert.Equal((byte)0, plan.DesiredLegacyMasksByPort["p2"]);
        Assert.Equal((byte)NesButton.A, plan.GetDesiredLegacyMask("p1"));
        Assert.Equal((byte)0, plan.GetDesiredLegacyMask("p2"));
        var write = Assert.Single(plan.WriteRequests);
        Assert.Equal("p1", write.PortId);
        Assert.Equal("a", write.ActionId);
        Assert.True(write.DesiredPressed);
        Assert.Equal(1f, write.Value);

        var writer = new FakeInputStateWriter();
        var maskUpdates = new List<(string PortId, string ActionId, bool Pressed)>();
        runtimeController.ApplyPlan(
            plan,
            new object(),
            writer,
            (portId, actionId, pressed) => maskUpdates.Add((portId, actionId, pressed)));

        var call = Assert.Single(writer.Calls);
        Assert.Equal("p1", call.PortId);
        Assert.Equal("a", call.ActionId);
        Assert.Equal(1f, call.Value);
        Assert.Equal([("p1", "a", true)], maskUpdates);
    }

    [Fact]
    public void BuildApplyPlan_WhenDesiredMaskIsZero_ReleasesCurrentMaskButtons()
    {
        var runtimeController = new MainWindowActiveInputRuntimeController();
        var inputStateController = new MainWindowInputStateController();
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/test.nes");
        runtimeController.PressKey(Key.Z);

        var activatePlan = runtimeController.BuildApplyPlan(
            inputStateController,
            new Dictionary<Key, (string PortId, string ActionId)>
            {
                [Key.Z] = ("p1", NesInputTestAdapter.ActionId(NesButton.A))
            },
            [],
            inputBindingSchema);
        runtimeController.ApplyPlan(
            activatePlan,
            new object(),
            new FakeInputStateWriter(),
            static (_, _, _) => { });

        runtimeController.ReleaseKey(Key.Z);

        var plan = runtimeController.BuildApplyPlan(
            inputStateController,
            new Dictionary<Key, (string PortId, string ActionId)>(),
            [],
            inputBindingSchema);

        var write = Assert.Single(plan.WriteRequests);
        Assert.Equal((byte)0, plan.GetDesiredLegacyMask("p1"));
        Assert.Equal("p1", write.PortId);
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
            [new ResolvedExtraInputBinding("p1", Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A))]);

        runtimeController.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/another.nes");

        Assert.False(runtimeController.TurboPulseActive);
        Assert.Empty(runtimeController.PressedKeys);
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
