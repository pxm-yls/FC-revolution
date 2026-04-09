using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendContractFacadeTests
{
    [Fact]
    public async Task SetButtonStateAsync_WithActionId_DelegatesToGenericInputPath()
    {
        var bridge = new RecordingRuntimeBridge
        {
            SetInputStateResult = true
        };
        var facade = new BackendContractFacade(new BackendRuntimeState(), bridge, bridge);
        var sessionId = Guid.NewGuid();

        var changed = await facade.SetButtonStateAsync(
            sessionId,
            new ButtonStateRequest(Player: 1, Pressed: true, PortId: "p2", ActionId: "right"));

        Assert.True(changed);
        var inputCall = Assert.Single(bridge.InputStateCalls);
        Assert.Equal(sessionId, inputCall.SessionId);
        var action = Assert.Single(inputCall.Request.Actions);
        Assert.Equal("p2", action.PortId);
        Assert.Equal("right", action.ActionId);
        Assert.Equal(1f, action.Value);
        Assert.Empty(bridge.ButtonCalls);
    }

    [Fact]
    public async Task SetButtonStateAsync_WithLegacyButton_FallsBackToLegacyPath()
    {
        var bridge = new RecordingRuntimeBridge
        {
            SetButtonStateResult = true
        };
        var facade = new BackendContractFacade(new BackendRuntimeState(), bridge, bridge);
        var sessionId = Guid.NewGuid();

        var changed = await facade.SetButtonStateAsync(
            sessionId,
            new ButtonStateRequest(Player: 0, Button: NesButtonDto.A, Pressed: true));

        Assert.True(changed);
        var buttonCall = Assert.Single(bridge.ButtonCalls);
        Assert.Equal(sessionId, buttonCall.SessionId);
        Assert.Equal(NesButtonDto.A, buttonCall.Request.Button);
        Assert.True(buttonCall.Request.Pressed);
        Assert.Empty(bridge.InputStateCalls);
    }
}
