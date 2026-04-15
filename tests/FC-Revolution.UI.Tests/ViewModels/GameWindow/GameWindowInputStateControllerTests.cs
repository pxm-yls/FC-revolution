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
            desiredMask: (byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB),
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

        var snapshot = controller.Snapshot;
        Assert.Equal((byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB), snapshot.GetCombinedMask("p1"));
        Assert.Equal((byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB), snapshot.GetLocalMask("p1"));
    }

    [Fact]
    public void ApplyDesiredLocalInputMask_ClearsStoredLocalMask_WhenLocalInputBlocked()
    {
        var controller = new GameWindowInputStateController();
        _ = controller.ApplyDesiredLocalInputMask("p1", FallbackInputTestData.MaskA, allowLocalInput: true);

        var changes = controller.ApplyDesiredLocalInputMask(
            portId: "p1",
            desiredMask: (byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB),
            allowLocalInput: false);

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal("p1", change.PortId);
                Assert.Equal(FallbackInputTestData.ActionA, change.ActionId);
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
        _ = controller.ApplyDesiredLocalInputMask("p1", FallbackInputTestData.MaskA, allowLocalInput: true);

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

        var snapshot = controller.Snapshot;
        Assert.Equal(FallbackInputTestData.MaskA, snapshot.GetCombinedMask("p1"));
        Assert.Equal(FallbackInputTestData.MaskA, snapshot.GetLocalMask("p1"));
        Assert.Equal(0, snapshot.GetRemoteMask("p1"));
    }
}
