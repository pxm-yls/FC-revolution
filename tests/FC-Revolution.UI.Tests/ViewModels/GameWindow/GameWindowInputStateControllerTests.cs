using FCRevolution.Core.Input;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowInputStateControllerTests
{
    [Fact]
    public void ApplyDesiredLocalInputMask_UpdatesCombinedMask_WhenLocalInputAllowed()
    {
        var controller = new GameWindowInputStateController();

        var changes = controller.ApplyDesiredLocalInputMask(
            portId: "p1",
            desiredMask: (byte)((byte)NesButton.A | (byte)NesButton.B),
            allowLocalInput: true);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.True(change.Pressed);
            },
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.True(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), snapshot.GetCombinedMask("p1"));
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), snapshot.GetLocalMask("p1"));
    }

    [Fact]
    public void ApplyDesiredLocalInputMask_ClearsStoredLocalMask_WhenLocalInputBlocked()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputMask("p1", (byte)NesButton.A, allowLocalInput: true);

        var changes = controller.ApplyDesiredLocalInputMask(
            portId: "p1",
            desiredMask: (byte)((byte)NesButton.A | (byte)NesButton.B),
            allowLocalInput: false);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.False(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal(0, snapshot.GetCombinedMask("p1"));
        Assert.Equal(0, snapshot.GetLocalMask("p1"));
    }

    [Fact]
    public void RemoteTransitions_RebuildCombinedMask_AcrossAcquireAndRelease()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputMask("p1", (byte)NesButton.A, allowLocalInput: true);

        var acquireChanges = controller.RebuildCombinedState("p1", allowLocalInput: false);
        Assert.Collection(
            acquireChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.False(change.Pressed);
            });

        var remoteChanges = controller.SetRemoteActionState("p1", NesInputTestAdapter.ActionId(NesButton.B), pressed: true, allowLocalInput: false);
        Assert.Collection(
            remoteChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.True(change.Pressed);
            });

        var clearChanges = controller.ClearRemoteButtons("p1", allowLocalInput: false);
        Assert.Collection(
            clearChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.False(change.Pressed);
            });

        var releaseChanges = controller.RebuildCombinedState("p1", allowLocalInput: true);
        Assert.Collection(
            releaseChanges,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.True(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal((byte)NesButton.A, snapshot.GetCombinedMask("p1"));
        Assert.Equal((byte)NesButton.A, snapshot.GetLocalMask("p1"));
        Assert.Equal(0, snapshot.GetRemoteMask("p1"));
    }
}
