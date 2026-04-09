using System;

namespace FCRevolution.Contracts.RemoteControl;

public static class RemoteControlPorts
{
    public const string Player1 = "p1";
    public const string Player2 = "p2";

    public static bool IsSupportedPlayer(int player) => player is 0 or 1;

    public static bool TryGetPlayer(string? portId, out int player)
    {
        if (string.Equals(portId, Player1, StringComparison.OrdinalIgnoreCase))
        {
            player = 0;
            return true;
        }

        if (string.Equals(portId, Player2, StringComparison.OrdinalIgnoreCase))
        {
            player = 1;
            return true;
        }

        player = default;
        return false;
    }

    public static bool TryGetPortId(int player, out string portId)
    {
        switch (player)
        {
            case 0:
                portId = Player1;
                return true;
            case 1:
                portId = Player2;
                return true;
            default:
                portId = string.Empty;
                return false;
        }
    }

    public static string? NormalizePortId(string? portId)
    {
        if (!TryGetPlayer(portId, out var player))
            return null;

        _ = TryGetPortId(player, out var normalizedPortId);
        return normalizedPortId;
    }
}
