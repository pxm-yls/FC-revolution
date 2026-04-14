using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Infrastructure;

internal sealed record CoreBindableInputAction(
    int Player,
    string PortId,
    string ActionId,
    string DisplayName);

internal sealed class CoreInputBindingSchema
{
    private static readonly IReadOnlyDictionary<int, string> FallbackPortDisplayNames =
        new ReadOnlyDictionary<int, string>(
            new Dictionary<int, string>
            {
                [0] = "1P",
                [1] = "2P"
            });

    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> PreferredKeyMaps =
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

    private static readonly IReadOnlyList<CoreBindableInputAction> FallbackActions =
    [
        new(0, "p1", "a", "A"),
        new(0, "p1", "b", "B"),
        new(0, "p1", "select", "Select"),
        new(0, "p1", "start", "Start"),
        new(0, "p1", "up", "Up"),
        new(0, "p1", "down", "Down"),
        new(0, "p1", "left", "Left"),
        new(0, "p1", "right", "Right"),
        new(1, "p2", "a", "A"),
        new(1, "p2", "b", "B"),
        new(1, "p2", "select", "Select"),
        new(1, "p2", "start", "Start"),
        new(1, "p2", "up", "Up"),
        new(1, "p2", "down", "Down"),
        new(1, "p2", "left", "Left"),
        new(1, "p2", "right", "Right")
    ];
    private static readonly IReadOnlyDictionary<string, byte> FallbackLegacyBits =
        new ReadOnlyDictionary<string, byte>(
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = 0x01,
                ["b"] = 0x02,
                ["select"] = 0x04,
                ["start"] = 0x08,
                ["up"] = 0x10,
                ["down"] = 0x20,
                ["left"] = 0x40,
                ["right"] = 0x80
            });

    private readonly IReadOnlyDictionary<int, IReadOnlyList<CoreBindableInputAction>> _actionsByPlayer;
    private readonly IReadOnlyDictionary<int, string> _portIdsByPlayer;
    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> _canonicalActionsByPlayer;
    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, byte>> _legacyBitMasksByPlayer;
    private readonly IReadOnlyDictionary<string, string> _displayNamesByActionId;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _supportedActionsByPort;
    private readonly IReadOnlyDictionary<string, (int Player, string PortId)> _portsById;
    private readonly IReadOnlyList<InputPortDescriptor> _supportedPorts;

    private CoreInputBindingSchema(
        IReadOnlyDictionary<int, IReadOnlyList<CoreBindableInputAction>> actionsByPlayer,
        IReadOnlyDictionary<int, string> portIdsByPlayer,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> canonicalActionsByPlayer,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, byte>> legacyBitMasksByPlayer,
        IReadOnlyDictionary<string, string> displayNamesByActionId,
        IReadOnlyDictionary<string, IReadOnlySet<string>> supportedActionsByPort,
        IReadOnlyDictionary<string, (int Player, string PortId)> portsById,
        IReadOnlyList<InputPortDescriptor> supportedPorts)
    {
        _actionsByPlayer = actionsByPlayer;
        _portIdsByPlayer = portIdsByPlayer;
        _canonicalActionsByPlayer = canonicalActionsByPlayer;
        _legacyBitMasksByPlayer = legacyBitMasksByPlayer;
        _displayNamesByActionId = displayNamesByActionId;
        _supportedActionsByPort = supportedActionsByPort;
        _portsById = portsById;
        _supportedPorts = supportedPorts;
        ExtraInputButtonOptions = BuildExtraInputButtonOptions(actionsByPlayer);
    }

    public IReadOnlyList<ExtraInputButtonOption> ExtraInputButtonOptions { get; }

    public static CoreInputBindingSchema CreateFallback() => Create(new EmptyInputSchema());

    public static CoreInputBindingSchema Create(IInputSchema inputSchema)
    {
        ArgumentNullException.ThrowIfNull(inputSchema);

        var inputPortsById = inputSchema.Ports
            .Where(static port => port.PlayerIndex is 0 or 1)
            .ToDictionary(port => port.PortId, port => port, StringComparer.OrdinalIgnoreCase);
        var actionsByPlayer = new Dictionary<int, List<CoreBindableInputAction>>
        {
            [0] = [],
            [1] = []
        };
        var portIdsByPlayer = new Dictionary<int, string>()
        {
            [0] = "p1",
            [1] = "p2"
        };
        var canonicalActionsByPlayer = new Dictionary<int, Dictionary<string, string>>
        {
            [0] = new(StringComparer.OrdinalIgnoreCase),
            [1] = new(StringComparer.OrdinalIgnoreCase)
        };
        var legacyBitMasksByPlayer = new Dictionary<int, Dictionary<string, byte>>
        {
            [0] = new(StringComparer.OrdinalIgnoreCase),
            [1] = new(StringComparer.OrdinalIgnoreCase)
        };
        var displayNamesByActionId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var supportedActionsByPort = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var port in inputPortsById.Values)
            portIdsByPlayer[port.PlayerIndex] = port.PortId;

        foreach (var action in inputSchema.Actions)
        {
            if (action.ValueKind != InputValueKind.Digital ||
                !inputPortsById.TryGetValue(action.PortId, out var port))
            {
                continue;
            }

            if (!supportedActionsByPort.TryGetValue(action.PortId, out var supportedActions))
            {
                supportedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                supportedActionsByPort[action.PortId] = supportedActions;
            }

            supportedActions.Add(action.ActionId);

            var canonicalActionId = ResolveCanonicalActionId(action);
            if (!string.IsNullOrWhiteSpace(canonicalActionId))
            {
                canonicalActionsByPlayer[port.PlayerIndex][action.ActionId] = canonicalActionId;

                if (action.LegacyBitMask is { } legacyBitMask)
                    legacyBitMasksByPlayer[port.PlayerIndex][canonicalActionId] = legacyBitMask;
            }

            if (!action.IsBindable || string.IsNullOrWhiteSpace(canonicalActionId))
                continue;

            if (actionsByPlayer[port.PlayerIndex].Any(existing =>
                    existing.ActionId.Equals(canonicalActionId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var bindableAction = new CoreBindableInputAction(
                port.PlayerIndex,
                action.PortId,
                canonicalActionId,
                action.DisplayName);
            actionsByPlayer[port.PlayerIndex].Add(bindableAction);
            displayNamesByActionId[canonicalActionId] = action.DisplayName;
        }

        if (actionsByPlayer.Values.All(static actions => actions.Count == 0))
        {
            foreach (var fallbackAction in FallbackActions)
            {
                actionsByPlayer[fallbackAction.Player].Add(fallbackAction);
                canonicalActionsByPlayer[fallbackAction.Player][fallbackAction.ActionId] = fallbackAction.ActionId;
                displayNamesByActionId[fallbackAction.ActionId] = fallbackAction.DisplayName;
                if (FallbackLegacyBits.TryGetValue(fallbackAction.ActionId, out var bit))
                    legacyBitMasksByPlayer[fallbackAction.Player][fallbackAction.ActionId] = bit;
            }
        }

        var portsById = portIdsByPlayer.ToDictionary(
            pair => pair.Value,
            pair => (pair.Key, pair.Value),
            StringComparer.OrdinalIgnoreCase);
        var supportedPorts = portIdsByPlayer
            .OrderBy(pair => pair.Key)
            .Select(pair =>
            {
                var matchingPort = inputPortsById.Values.FirstOrDefault(port => port.PlayerIndex == pair.Key);
                var displayName = matchingPort?.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = FallbackPortDisplayNames.TryGetValue(pair.Key, out var fallbackDisplayName)
                        ? fallbackDisplayName
                        : pair.Value;
                }

                return new InputPortDescriptor(pair.Value, displayName, pair.Key);
            })
            .ToArray();

        return new CoreInputBindingSchema(
            actionsByPlayer.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CoreBindableInputAction>)pair.Value,
                EqualityComparer<int>.Default),
            new ReadOnlyDictionary<int, string>(portIdsByPlayer),
            canonicalActionsByPlayer.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(pair.Value),
                EqualityComparer<int>.Default),
            legacyBitMasksByPlayer.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, byte>)new ReadOnlyDictionary<string, byte>(pair.Value),
                EqualityComparer<int>.Default),
            new ReadOnlyDictionary<string, string>(displayNamesByActionId),
            supportedActionsByPort.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlySet<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            new ReadOnlyDictionary<string, (int Player, string PortId)>(portsById),
            supportedPorts);
    }

    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> BuildDefaultKeyMaps(IReadOnlyList<Key> configurableKeys)
    {
        ArgumentNullException.ThrowIfNull(configurableKeys);

        var defaultKeyMaps = new Dictionary<int, IReadOnlyDictionary<string, Key>>();
        foreach (var player in GetSupportedPlayers())
        {
            var usedKeys = new HashSet<Key>();
            var playerDefaultKeyMap = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in GetBindableActions(player))
            {
                if (TryGetPreferredKey(player, action.ActionId, configurableKeys, usedKeys, out var preferredKey))
                {
                    playerDefaultKeyMap[action.ActionId] = preferredKey;
                    continue;
                }

                var fallbackKey = configurableKeys.FirstOrDefault(key => !usedKeys.Contains(key));
                playerDefaultKeyMap[action.ActionId] = fallbackKey;
                if (fallbackKey != Key.None)
                    usedKeys.Add(fallbackKey);
            }

            defaultKeyMaps[player] = new ReadOnlyDictionary<string, Key>(playerDefaultKeyMap);
        }

        return new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>>(defaultKeyMaps);
    }

    public IReadOnlyList<CoreBindableInputAction> GetBindableActions(int player) =>
        _actionsByPlayer.TryGetValue(player, out var actions) ? actions : Array.Empty<CoreBindableInputAction>();

    public IReadOnlyList<InputPortDescriptor> GetSupportedPorts() => _supportedPorts;

    public IReadOnlyList<string> GetBindableActionIds(int player) =>
        GetBindableActions(player).Select(static action => action.ActionId).ToArray();

    public string GetPortId(int player) =>
        _portIdsByPlayer.TryGetValue(player, out var portId) ? portId : $"p{player + 1}";

    public bool TryGetPlayer(string? portId, out int player)
    {
        player = default;
        if (string.IsNullOrWhiteSpace(portId) ||
            !_portsById.TryGetValue(portId.Trim(), out var port))
        {
            return false;
        }

        player = port.Player;
        return true;
    }

    public bool TryNormalizePortId(string? portId, out string normalizedPortId)
    {
        normalizedPortId = string.Empty;
        if (string.IsNullOrWhiteSpace(portId) ||
            !_portsById.TryGetValue(portId.Trim(), out var port))
        {
            return false;
        }

        normalizedPortId = port.PortId;
        return true;
    }

    public bool TryResolvePort(string? portId, out int player, out string normalizedPortId)
    {
        player = default;
        normalizedPortId = string.Empty;
        if (!TryNormalizePortId(portId, out normalizedPortId) ||
            !_portsById.TryGetValue(normalizedPortId, out var port))
        {
            return false;
        }

        player = port.Player;
        return true;
    }

    public bool TryNormalizeActionId(int player, string? actionId, out string normalizedActionId)
    {
        normalizedActionId = string.Empty;
        if (string.IsNullOrWhiteSpace(actionId) ||
            !_canonicalActionsByPlayer.TryGetValue(player, out var actions) ||
            !actions.TryGetValue(actionId.Trim(), out var resolvedActionId))
        {
            return false;
        }

        normalizedActionId = resolvedActionId;
        return true;
    }

    public bool TryGetDisplayName(string? actionId, out string displayName)
    {
        displayName = string.Empty;
        if (string.IsNullOrWhiteSpace(actionId) ||
            !_displayNamesByActionId.TryGetValue(actionId.Trim(), out var resolvedDisplayName))
        {
            return false;
        }

        displayName = resolvedDisplayName;
        return true;
    }

    public bool IsBindableAction(int player, string? actionId) =>
        TryNormalizeActionId(player, actionId, out var normalizedActionId) &&
        GetBindableActions(player).Any(action =>
            action.ActionId.Equals(normalizedActionId, StringComparison.OrdinalIgnoreCase));

    public bool TryGetLegacyBitMask(int player, string? actionId, out byte bitMask)
    {
        bitMask = 0;
        return TryNormalizeActionId(player, actionId, out var normalizedActionId) &&
            _legacyBitMasksByPlayer.TryGetValue(player, out var bitMasks) &&
            bitMasks.TryGetValue(normalizedActionId, out bitMask);
    }

    public bool IsSupportedInputAction(string? portId, string? actionId) =>
        !string.IsNullOrWhiteSpace(portId) &&
        !string.IsNullOrWhiteSpace(actionId) &&
        _supportedActionsByPort.TryGetValue(portId.Trim(), out var supportedActions) &&
        supportedActions.Contains(actionId.Trim());

    private static IReadOnlyList<ExtraInputButtonOption> BuildExtraInputButtonOptions(
        IReadOnlyDictionary<int, IReadOnlyList<CoreBindableInputAction>> actionsByPlayer)
    {
        var sourceActions = actionsByPlayer.TryGetValue(0, out var player1Actions) && player1Actions.Count > 0
            ? player1Actions
            : actionsByPlayer.Values.SelectMany(static actions => actions)
                .GroupBy(static action => action.ActionId, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray();

        return sourceActions
            .Select(static action => new ExtraInputButtonOption(action.ActionId, action.DisplayName))
            .ToArray();
    }

    private static string? ResolveCanonicalActionId(InputActionDescriptor action)
    {
        if (!string.IsNullOrWhiteSpace(action.ResolvedCanonicalActionId))
            return action.ResolvedCanonicalActionId!.Trim();

        return null;
    }

    private static IEnumerable<int> GetSupportedPlayers()
    {
        yield return 0;
        yield return 1;
    }

    private static bool TryGetPreferredKey(
        int player,
        string actionId,
        IReadOnlyList<Key> configurableKeys,
        ISet<Key> usedKeys,
        out Key preferredKey)
    {
        preferredKey = Key.None;
        return PreferredKeyMaps.TryGetValue(player, out var preferredKeyMap) &&
            preferredKeyMap.TryGetValue(actionId, out preferredKey) &&
            configurableKeys.Contains(preferredKey) &&
            usedKeys.Add(preferredKey);
    }

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } =
        [
            new("p1", "Player 1", 0),
            new("p2", "Player 2", 1)
        ];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}
