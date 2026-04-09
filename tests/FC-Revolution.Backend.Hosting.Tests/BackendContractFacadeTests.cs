using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendContractFacadeTests
{
    [Fact]
    public async Task SetInputStateAsync_Delegates_To_RemoteControlBridge()
    {
        var bridge = new RecordingRuntimeBridge
        {
            SetInputStateResult = true
        };
        var facade = new BackendContractFacade(new BackendRuntimeState(), bridge, bridge);
        var sessionId = Guid.NewGuid();

        var changed = await facade.SetInputStateAsync(
            sessionId,
            new SetInputStateRequest(
            [
                new InputActionValueDto("p2", "gamepad", "right", 1f)
            ]));

        Assert.True(changed);
        var inputCall = Assert.Single(bridge.InputStateCalls);
        Assert.Equal(sessionId, inputCall.SessionId);
        var action = Assert.Single(inputCall.Request.Actions);
        Assert.Equal("p2", action.PortId);
        Assert.Equal("right", action.ActionId);
        Assert.Equal(1f, action.Value);
    }
}
