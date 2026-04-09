using FCRevolution.Core.Input;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowInputStateControllerTests
{
    [Fact]
    public void ApplyDesiredLocalInputMaskForPlayer_UpdatesCombinedMask_WhenLocalInputAllowed()
    {
        var controller = new GameWindowInputStateController();

        var changes = controller.ApplyDesiredLocalInputMaskForPlayer(
            player: 0,
            desiredMask: (byte)((byte)NesButton.A | (byte)NesButton.B),
            allowLocalInput: true);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.True(change.Pressed);
            },
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.True(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), snapshot.Player1CombinedMask);
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), snapshot.Player1LocalMask);
    }

    [Fact]
    public void ApplyDesiredLocalInputMaskForPlayer_ClearsStoredLocalMask_WhenLocalInputBlocked()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputMaskForPlayer(0, (byte)NesButton.A, allowLocalInput: true);

        var changes = controller.ApplyDesiredLocalInputMaskForPlayer(
            player: 0,
            desiredMask: (byte)((byte)NesButton.A | (byte)NesButton.B),
            allowLocalInput: false);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.False(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal(0, snapshot.Player1CombinedMask);
        Assert.Equal(0, snapshot.Player1LocalMask);
    }

    [Fact]
    public void RemoteTransitions_RebuildCombinedMask_AcrossAcquireAndRelease()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputMaskForPlayer(0, (byte)NesButton.A, allowLocalInput: true);

        var acquireChanges = controller.RebuildCombinedStateForPlayer(0, allowLocalInput: false);
        Assert.Collection(
            acquireChanges,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.False(change.Pressed);
            });

        var remoteChanges = controller.SetRemoteActionState(0, NesInputTestAdapter.ActionId(NesButton.B), pressed: true, allowLocalInput: false);
        Assert.Collection(
            remoteChanges,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.True(change.Pressed);
            });

        var clearChanges = controller.ClearRemoteButtons(0, allowLocalInput: false);
        Assert.Collection(
            clearChanges,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), change.ActionId);
                Assert.False(change.Pressed);
            });

        var releaseChanges = controller.RebuildCombinedStateForPlayer(0, allowLocalInput: true);
        Assert.Collection(
            releaseChanges,
            change =>
            {
                Assert.Equal(0, change.Player);
                Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), change.ActionId);
                Assert.True(change.Pressed);
            });

        var snapshot = controller.Snapshot;
        Assert.Equal((byte)NesButton.A, snapshot.Player1CombinedMask);
        Assert.Equal((byte)NesButton.A, snapshot.Player1LocalMask);
        Assert.Equal(0, snapshot.Player1RemoteMask);
    }
}
