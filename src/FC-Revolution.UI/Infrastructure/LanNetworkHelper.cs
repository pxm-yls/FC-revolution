using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FC_Revolution.UI.Infrastructure;

public static class LanNetworkHelper
{
    public static string? GetPreferredLanAddress()
    {
        try
        {
            return GetCandidateLanAddresses().FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<string> GetCandidateLanAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsUsableInterface)
                .OrderBy(GetInterfacePriority)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .Where(IsUsableLanAddress)
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsUsableInterface(NetworkInterface network)
    {
        if (network.OperationalStatus != OperationalStatus.Up)
            return false;

        if (network.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown)
            return false;

        var name = $"{network.Name} {network.Description}".ToLowerInvariant();
        if (name.Contains("virtual") || name.Contains("vmware") || name.Contains("hyper-v") || name.Contains("vbox") || name.Contains("docker") || name.Contains("tailscale"))
            return false;

        return network.GetIPProperties().GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork);
    }

    private static bool IsUsableLanAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
            return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private static int GetInterfacePriority(NetworkInterface network)
    {
        return network.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 0,
            NetworkInterfaceType.Ethernet => 1,
            NetworkInterfaceType.GigabitEthernet => 1,
            _ => 10
        };
    }
}
