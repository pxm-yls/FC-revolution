using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;

namespace FC_Revolution.UI.Adapters.Nes;

internal sealed record NesInputActionDescriptor(
    string ActionId,
    string DisplayName,
    byte BitMask);

internal static class NesInputAdapter
{
    private static readonly IReadOnlyList<NesInputActionDescriptor> ControllerActions =
    [
        new("a", "A", 0x01),
        new("b", "B", 0x02),
        new("select", "Select", 0x04),
        new("start", "Start", 0x08),
        new("up", "Up", 0x10),
        new("down", "Down", 0x20),
        new("left", "Left", 0x40),
        new("right", "Right", 0x80)
    ];

    private static readonly IReadOnlyDictionary<string, NesInputActionDescriptor> ActionsById =
        new ReadOnlyDictionary<string, NesInputActionDescriptor>(
            ControllerActions.ToDictionary(
                action => action.ActionId,
                action => action,
                StringComparer.OrdinalIgnoreCase));

    private static readonly IReadOnlyDictionary<string, string> RemoteCompatibilityActionMap =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = "a",
                ["x"] = "a",
                ["turboa"] = "a",
                ["b"] = "b",
                ["y"] = "b",
                ["turbob"] = "b",
                ["select"] = "select",
                ["start"] = "start",
                ["up"] = "up",
                ["down"] = "down",
                ["left"] = "left",
                ["right"] = "right"
            });

    private static readonly HashSet<string> ReservedRemoteCompatibilityActionIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "l1",
            "r1",
            "l2",
            "r2",
            "l3",
            "r3"
        };

    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> DefaultKeyMaps =
        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>>(
            new Dictionary<int, IReadOnlyDictionary<string, Key>>
            {
                [0] = new ReadOnlyDictionary<string, Key>(
                    new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["a"] = Key.Z,
                        ["b"] = Key.X,
                        ["select"] = Key.A,
                        ["start"] = Key.S,
                        ["up"] = Key.Up,
                        ["down"] = Key.Down,
                        ["left"] = Key.Left,
                        ["right"] = Key.Right
                    }),
                [1] = new ReadOnlyDictionary<string, Key>(
                    new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["a"] = Key.U,
                        ["b"] = Key.O,
                        ["select"] = Key.RightCtrl,
                        ["start"] = Key.Enter,
                        ["up"] = Key.I,
                        ["down"] = Key.K,
                        ["left"] = Key.J,
                        ["right"] = Key.L
                    })
            });

    public static IReadOnlyList<NesInputActionDescriptor> GetControllerActions() => ControllerActions;

    public static IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> GetDefaultKeyMaps() => DefaultKeyMaps;

    public static bool IsControllerAction(string? actionId) =>
        !string.IsNullOrWhiteSpace(actionId) && ActionsById.ContainsKey(actionId.Trim());

    public static bool TryNormalizeControllerAction(string? actionId, out string normalizedActionId)
    {
        normalizedActionId = string.Empty;
        return TryGetDescriptor(actionId, out var descriptor) && TryAssignNormalizedActionId(descriptor, out normalizedActionId);
    }

    public static bool TryGetDescriptor(string? actionId, out NesInputActionDescriptor descriptor)
    {
        descriptor = null!;
        return !string.IsNullOrWhiteSpace(actionId) &&
            ActionsById.TryGetValue(actionId.Trim(), out descriptor!);
    }

    public static bool TryGetBitMask(string? actionId, out byte bitMask)
    {
        bitMask = 0;
        return TryGetDescriptor(actionId, out var descriptor) && TryAssignBitMask(descriptor, out bitMask);
    }

    public static bool TryGetDisplayName(string? actionId, out string displayName)
    {
        displayName = string.Empty;
        return TryGetDescriptor(actionId, out var descriptor) && TryAssignDisplayName(descriptor, out displayName);
    }

    public static bool TryMapRemoteCompatibilityAction(
        string? actionId,
        out string normalizedActionId,
        out bool isReserved)
    {
        normalizedActionId = string.Empty;
        isReserved = false;

        if (string.IsNullOrWhiteSpace(actionId))
            return false;

        var trimmed = actionId.Trim();
        if (RemoteCompatibilityActionMap.TryGetValue(trimmed, out var mappedActionId))
        {
            normalizedActionId = mappedActionId;
            return true;
        }

        if (ReservedRemoteCompatibilityActionIds.Contains(trimmed))
        {
            isReserved = true;
            return true;
        }

        return false;
    }

    private static bool TryAssignBitMask(NesInputActionDescriptor descriptor, out byte bitMask)
    {
        bitMask = descriptor.BitMask;
        return true;
    }

    private static bool TryAssignDisplayName(NesInputActionDescriptor descriptor, out string displayName)
    {
        displayName = descriptor.DisplayName;
        return true;
    }

    private static bool TryAssignNormalizedActionId(NesInputActionDescriptor descriptor, out string normalizedActionId)
    {
        normalizedActionId = descriptor.ActionId;
        return true;
    }
}
