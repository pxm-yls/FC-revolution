using System.Collections.Generic;
using System.Reflection;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowInputStateControllerTests
{
    [Fact]
    public void ApplyDesiredLocalInputActions_UpdatesCombinedMask_WhenLocalInputAllowed()
    {
        var controller = new GameWindowInputStateController();

        var changes = controller.ApplyDesiredLocalInputActions(
            portId: "p1",
            desiredActions: ActionSet(FallbackInputTestData.ActionA, FallbackInputTestData.ActionB),
            allowLocalInput: true);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionA, change.ActionId);
                Assert.True(change.Pressed);
            },
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionB, change.ActionId);
                Assert.True(change.Pressed);
            });

        Assert.Equal((byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB), ReadCombinedMask(controller, "p1"));
    }

    [Fact]
    public void ApplyDesiredLocalInputActions_ClearsStoredLocalState_WhenLocalInputBlocked()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputActions(
            "p1",
            ActionSet(FallbackInputTestData.ActionA),
            allowLocalInput: true);

        var changes = controller.ApplyDesiredLocalInputActions(
            portId: "p1",
            desiredActions: ActionSet(FallbackInputTestData.ActionA, FallbackInputTestData.ActionB),
            allowLocalInput: false);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionA, change.ActionId);
                Assert.False(change.Pressed);
            });

        Assert.Equal(0, ReadCombinedMask(controller, "p1"));
    }

    [Fact]
    public void RemoteTransitions_RebuildCombinedMask_AcrossAcquireAndRelease()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputActions(
            "p1",
            ActionSet(FallbackInputTestData.ActionA),
            allowLocalInput: true);

        var acquireChanges = controller.RebuildCombinedState("p1", allowLocalInput: false);
        Assert.Collection(
            acquireChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionA, change.ActionId);
                Assert.False(change.Pressed);
            });

        var remoteChanges = controller.SetRemoteActionState("p1", FallbackInputTestData.ActionB, pressed: true, allowLocalInput: false);
        Assert.Collection(
            remoteChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionB, change.ActionId);
                Assert.True(change.Pressed);
            });

        var clearChanges = controller.ClearRemoteButtons("p1", allowLocalInput: false);
        Assert.Collection(
            clearChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionB, change.ActionId);
                Assert.False(change.Pressed);
            });

        var releaseChanges = controller.RebuildCombinedState("p1", allowLocalInput: true);
        Assert.Collection(
            releaseChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionA, change.ActionId);
                Assert.True(change.Pressed);
            });

        Assert.Equal(FallbackInputTestData.MaskA, ReadCombinedMask(controller, "p1"));
    }

    private static IReadOnlySet<string> ActionSet(params string[] actionIds) =>
        new HashSet<string>(actionIds, System.StringComparer.OrdinalIgnoreCase);

    private static byte ReadCombinedMask(GameWindowInputStateController controller, string portId)
    {
        var schemaField = typeof(GameWindowInputStateController).GetField(
            "_inputBindingSchema",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(schemaField);
        var inputBindingSchema = Assert.IsType<CoreInputBindingSchema>(schemaField!.GetValue(controller));

        var combinedActionsField = typeof(GameWindowInputStateController).GetField(
            "_combinedActionsByPort",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(combinedActionsField);
        var combinedActionsByPort = Assert.IsAssignableFrom<Dictionary<string, HashSet<string>>>(combinedActionsField!.GetValue(controller));
        if (!combinedActionsByPort.TryGetValue(portId, out var actions))
            return 0;

        byte mask = 0;
        foreach (var actionId in actions)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(portId, actionId, out var bit))
                mask |= bit;
        }

        return mask;
    }
}
