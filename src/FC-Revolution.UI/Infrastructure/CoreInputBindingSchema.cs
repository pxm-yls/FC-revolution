using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Infrastructure;

internal sealed record CoreBindableInputAction(
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
        new("p1", "a", "A"),
        new("p1", "b", "B"),
        new("p1", "select", "Select"),
        new("p1", "start", "Start"),
        new("p1", "up", "Up"),
        new("p1", "down", "Down"),
        new("p1", "left", "Left"),
        new("p1", "right", "Right"),
        new("p2", "a", "A"),
        new("p2", "b", "B"),
        new("p2", "select", "Select"),
        new("p2", "start", "Start"),
        new("p2", "up", "Up"),
        new("p2", "down", "Down"),
        new("p2", "left", "Left"),
        new("p2", "right", "Right")
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

    private readonly IReadOnlyDictionary<string, IReadOnlyList<CoreBindableInputAction>> _actionsByPort;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _canonicalActionsByPort;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte>> _legacyBitMasksByPort;
    private readonly IReadOnlyDictionary<string, string> _displayNamesByActionId;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _supportedActionsByPort;
    private readonly IReadOnlyDictionary<string, string> _portsById;
    private readonly IReadOnlyList<InputPortDescriptor> _supportedPorts;

    private CoreInputBindingSchema(
        IReadOnlyDictionary<string, IReadOnlyList<CoreBindableInputAction>> actionsByPort,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> canonicalActionsByPort,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte>> legacyBitMasksByPort,
        IReadOnlyDictionary<string, string> displayNamesByActionId,
        IReadOnlyDictionary<string, IReadOnlySet<string>> supportedActionsByPort,
        IReadOnlyDictionary<string, string> portsById,
        IReadOnlyList<InputPortDescriptor> supportedPorts)
    {
        _actionsByPort = actionsByPort;
        _canonicalActionsByPort = canonicalActionsByPort;
        _legacyBitMasksByPort = legacyBitMasksByPort;
        _displayNamesByActionId = displayNamesByActionId;
        _supportedActionsByPort = supportedActionsByPort;
        _portsById = portsById;
        _supportedPorts = supportedPorts;
        ExtraInputButtonOptions = BuildExtraInputButtonOptions(supportedPorts, actionsByPort);
    }

    public IReadOnlyList<ExtraInputButtonOption> ExtraInputButtonOptions { get; }

    public static CoreInputBindingSchema CreateFallback() => Create(new EmptyInputSchema());

    public static CoreInputBindingSchema Create(IInputSchema inputSchema)
    {
        ArgumentNullException.ThrowIfNull(inputSchema);

        if (inputSchema is EmptyInputSchema)
            return CreateFallbackSchema();

        var supportedPorts = inputSchema.Ports
            .Where(static port => !string.IsNullOrWhiteSpace(port.PortId))
            .Select(static port => new InputPortDescriptor(
                port.PortId.Trim(),
                ResolvePortDisplayName(port),
                port.PlayerIndex))
            .GroupBy(static port => port.PortId, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static port => port.PlayerIndex)
            .ThenBy(static port => port.PortId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var inputPortsById = supportedPorts.ToDictionary(static port => port.PortId, StringComparer.OrdinalIgnoreCase);
        var actionsByPort = new Dictionary<string, List<CoreBindableInputAction>>(StringComparer.OrdinalIgnoreCase);
        var canonicalActionsByPort = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var legacyBitMasksByPort = new Dictionary<string, Dictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);
        var displayNamesByActionId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var supportedActionsByPort = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var port in supportedPorts)
        {
            actionsByPort[port.PortId] = [];
            canonicalActionsByPort[port.PortId] = new(StringComparer.OrdinalIgnoreCase);
            legacyBitMasksByPort[port.PortId] = new(StringComparer.OrdinalIgnoreCase);
            supportedActionsByPort[port.PortId] = new(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var action in inputSchema.Actions)
        {
            if (action.ValueKind != InputValueKind.Digital ||
                string.IsNullOrWhiteSpace(action.PortId) ||
                !inputPortsById.TryGetValue(action.PortId.Trim(), out var port))
            {
                continue;
            }

            var supportedActions = supportedActionsByPort[port.PortId];
            supportedActions.Add(action.ActionId);

            var canonicalActionId = ResolveCanonicalActionId(action);
            if (!string.IsNullOrWhiteSpace(canonicalActionId))
            {
                canonicalActionsByPort[port.PortId][action.ActionId] = canonicalActionId;

                if (action.LegacyBitMask is { } legacyBitMask)
                {
                    legacyBitMasksByPort[port.PortId][canonicalActionId] = legacyBitMask;
                }
            }

            if (!action.IsBindable || string.IsNullOrWhiteSpace(canonicalActionId))
                continue;

            if (actionsByPort[port.PortId].Any(existing =>
                    existing.ActionId.Equals(canonicalActionId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var bindableAction = new CoreBindableInputAction(
                port.PortId,
                canonicalActionId,
                action.DisplayName);
            actionsByPort[port.PortId].Add(bindableAction);
            displayNamesByActionId[canonicalActionId] = action.DisplayName;
        }

        var portsById = supportedPorts.ToDictionary(
            static port => port.PortId,
            static port => port.PortId,
            StringComparer.OrdinalIgnoreCase);

        return new CoreInputBindingSchema(
            actionsByPort.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CoreBindableInputAction>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            canonicalActionsByPort.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            legacyBitMasksByPort.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, byte>)new ReadOnlyDictionary<string, byte>(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            new ReadOnlyDictionary<string, string>(displayNamesByActionId),
            supportedActionsByPort.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlySet<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            new ReadOnlyDictionary<string, string>(portsById),
            supportedPorts);
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> BuildDefaultKeyMapsByPort(IReadOnlyList<Key> configurableKeys)
    {
        ArgumentNullException.ThrowIfNull(configurableKeys);

        var defaultKeyMaps = new Dictionary<string, IReadOnlyDictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var port in GetSupportedPorts())
        {
            var usedKeys = new HashSet<Key>();
            var portDefaultKeyMap = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in GetBindableActions(port.PortId))
            {
                if (TryGetPreferredKey(port.PlayerIndex, action.ActionId, configurableKeys, usedKeys, out var preferredKey))
                {
                    portDefaultKeyMap[action.ActionId] = preferredKey;
                    continue;
                }

                var fallbackKey = configurableKeys.FirstOrDefault(key => !usedKeys.Contains(key));
                portDefaultKeyMap[action.ActionId] = fallbackKey;
                if (fallbackKey != Key.None)
                    usedKeys.Add(fallbackKey);
            }

            defaultKeyMaps[port.PortId] = new ReadOnlyDictionary<string, Key>(portDefaultKeyMap);
        }

        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>>(defaultKeyMaps);
    }

    public IReadOnlyList<CoreBindableInputAction> GetBindableActions(string? portId) =>
        TryNormalizePortId(portId, out var normalizedPortId) &&
        _actionsByPort.TryGetValue(normalizedPortId, out var actions)
            ? actions
            : Array.Empty<CoreBindableInputAction>();

    public IReadOnlyList<InputPortDescriptor> GetSupportedPorts() => _supportedPorts;

    public IReadOnlyList<string> GetBindableActionIds(string? portId) =>
        GetBindableActions(portId).Select(static action => action.ActionId).ToArray();

    public IReadOnlyList<string> GetSupportedActionIds(string? portId) =>
        TryNormalizePortId(portId, out var normalizedPortId) &&
        _supportedActionsByPort.TryGetValue(normalizedPortId, out var actions)
            ? actions.OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();

    public string GetPortDisplayName(string? portId)
    {
        if (TryNormalizePortId(portId, out var normalizedPortId))
        {
            var port = _supportedPorts.FirstOrDefault(candidate =>
                candidate.PortId.Equals(normalizedPortId, StringComparison.OrdinalIgnoreCase));
            if (port != null && !string.IsNullOrWhiteSpace(port.DisplayName))
                return port.DisplayName;
        }

        return string.IsNullOrWhiteSpace(portId) ? string.Empty : portId.Trim();
    }

    public bool TryNormalizePortId(string? portId, out string normalizedPortId)
    {
        normalizedPortId = string.Empty;
        if (string.IsNullOrWhiteSpace(portId) ||
            !_portsById.TryGetValue(portId.Trim(), out var portIdValue))
        {
            return false;
        }

        normalizedPortId = portIdValue;
        return true;
    }

    public bool TryNormalizeActionId(string? portId, string? actionId, out string normalizedActionId)
    {
        normalizedActionId = string.Empty;
        if (!TryNormalizePortId(portId, out var normalizedPortId) ||
            string.IsNullOrWhiteSpace(actionId) ||
            !_canonicalActionsByPort.TryGetValue(normalizedPortId, out var actions) ||
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

    public bool IsBindableAction(string? portId, string? actionId) =>
        TryNormalizeActionId(portId, actionId, out var normalizedActionId) &&
        GetBindableActions(portId).Any(action =>
            action.ActionId.Equals(normalizedActionId, StringComparison.OrdinalIgnoreCase));

    public bool TryGetLegacyBitMask(string? portId, string? actionId, out byte bitMask)
    {
        bitMask = 0;
        return TryNormalizeActionId(portId, actionId, out var normalizedActionId) &&
            TryNormalizePortId(portId, out var normalizedPortId) &&
            _legacyBitMasksByPort.TryGetValue(normalizedPortId, out var bitMasks) &&
            bitMasks.TryGetValue(normalizedActionId, out bitMask);
    }

    public bool IsSupportedInputAction(string? portId, string? actionId) =>
        !string.IsNullOrWhiteSpace(portId) &&
        !string.IsNullOrWhiteSpace(actionId) &&
        _supportedActionsByPort.TryGetValue(portId.Trim(), out var supportedActions) &&
        supportedActions.Contains(actionId.Trim());

    private static IReadOnlyList<ExtraInputButtonOption> BuildExtraInputButtonOptions(
        IReadOnlyList<InputPortDescriptor> supportedPorts,
        IReadOnlyDictionary<string, IReadOnlyList<CoreBindableInputAction>> actionsByPort)
    {
        var sourceActions = supportedPorts
                .Select(port => actionsByPort.TryGetValue(port.PortId, out var portActions) ? portActions : Array.Empty<CoreBindableInputAction>())
                .FirstOrDefault(static portActions => portActions.Count > 0)
            ?? actionsByPort.Values.SelectMany(static actions => actions)
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

    private static string ResolvePortDisplayName(InputPortDescriptor port)
    {
        if (!string.IsNullOrWhiteSpace(port.DisplayName))
            return port.DisplayName.Trim();

        return FallbackPortDisplayNames.TryGetValue(port.PlayerIndex, out var fallbackDisplayName)
            ? fallbackDisplayName
            : port.PortId.Trim();
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

    private static CoreInputBindingSchema CreateFallbackSchema()
    {
        var supportedPorts = new[]
        {
            new InputPortDescriptor("p1", "1P", 0),
            new InputPortDescriptor("p2", "2P", 1)
        };
        var actionsByPort = new Dictionary<string, IReadOnlyList<CoreBindableInputAction>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = FallbackActions.Where(static action => action.PortId == "p1").ToArray(),
            ["p2"] = FallbackActions.Where(static action => action.PortId == "p2").ToArray()
        };
        var canonicalActionsByPort = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new ReadOnlyDictionary<string, string>(
                actionsByPort["p1"].ToDictionary(static action => action.ActionId, static action => action.ActionId, StringComparer.OrdinalIgnoreCase)),
            ["p2"] = new ReadOnlyDictionary<string, string>(
                actionsByPort["p2"].ToDictionary(static action => action.ActionId, static action => action.ActionId, StringComparer.OrdinalIgnoreCase))
        };
        var legacyBitMasksByPort = new Dictionary<string, IReadOnlyDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new ReadOnlyDictionary<string, byte>(
                actionsByPort["p1"]
                    .Where(action => FallbackLegacyBits.ContainsKey(action.ActionId))
                    .ToDictionary(static action => action.ActionId, action => FallbackLegacyBits[action.ActionId], StringComparer.OrdinalIgnoreCase)),
            ["p2"] = new ReadOnlyDictionary<string, byte>(
                actionsByPort["p2"]
                    .Where(action => FallbackLegacyBits.ContainsKey(action.ActionId))
                    .ToDictionary(static action => action.ActionId, action => FallbackLegacyBits[action.ActionId], StringComparer.OrdinalIgnoreCase))
        };
        var displayNamesByActionId = new ReadOnlyDictionary<string, string>(
            FallbackActions
                .GroupBy(static action => action.ActionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First().DisplayName, StringComparer.OrdinalIgnoreCase));
        var supportedActionsByPort = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new HashSet<string>(actionsByPort["p1"].Select(static action => action.ActionId), StringComparer.OrdinalIgnoreCase),
            ["p2"] = new HashSet<string>(actionsByPort["p2"].Select(static action => action.ActionId), StringComparer.OrdinalIgnoreCase)
        };
        var portsById = new ReadOnlyDictionary<string, string>(
            supportedPorts.ToDictionary(static port => port.PortId, static port => port.PortId, StringComparer.OrdinalIgnoreCase));

        return new CoreInputBindingSchema(
            new ReadOnlyDictionary<string, IReadOnlyList<CoreBindableInputAction>>(actionsByPort),
            new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(canonicalActionsByPort),
            new ReadOnlyDictionary<string, IReadOnlyDictionary<string, byte>>(legacyBitMasksByPort),
            displayNamesByActionId,
            new ReadOnlyDictionary<string, IReadOnlySet<string>>(supportedActionsByPort),
            portsById,
            supportedPorts);
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
