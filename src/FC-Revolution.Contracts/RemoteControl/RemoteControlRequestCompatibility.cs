namespace FCRevolution.Contracts.RemoteControl;

public static class RemoteControlRequestCompatibility
{
    public static bool TryBuildGenericInputRequest(
        ButtonStateRequest request,
        string deviceType,
        out SetInputStateRequest genericRequest)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceType);

        genericRequest = default!;
        if (string.IsNullOrWhiteSpace(request.ActionId))
            return false;

        string? portId = null;
        if (!string.IsNullOrWhiteSpace(request.PortId))
        {
            portId = RemoteControlPorts.NormalizePortId(request.PortId);
            if (portId == null)
                return false;
        }

        string? mappedPortId = null;
        if (portId == null &&
            !RemoteControlPorts.TryGetPortId(request.Player, out mappedPortId))
        {
            return false;
        }

        portId ??= mappedPortId!;
        genericRequest = new SetInputStateRequest(
        [
            new InputActionValueDto(
                portId,
                deviceType.Trim(),
                request.ActionId.Trim(),
                request.Pressed ? 1f : 0f)
        ]);
        return true;
    }
}
