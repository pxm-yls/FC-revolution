using System.Collections.Generic;
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

        Assert.Equal((byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB), controller.GetCombinedMask("p1"));
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

        Assert.Equal(0, controller.GetCombinedMask("p1"));
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

        Assert.Equal(FallbackInputTestData.MaskA, controller.GetCombinedMask("p1"));
    }

    private static IReadOnlySet<string> ActionSet(params string[] actionIds) =>
        new HashSet<string>(actionIds, System.StringComparer.OrdinalIgnoreCase);
}
