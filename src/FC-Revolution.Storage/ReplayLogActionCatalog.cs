namespace FCRevolution.Storage;

internal static class ReplayLogActionCatalog
{
    private static readonly IReadOnlyList<string> DefaultLegacyActionIds =
    [
        "a",
        "b",
        "select",
        "start",
        "up",
        "down",
        "left",
        "right"
    ];

    public static IReadOnlyList<ReplayLogPortLayout> CreateDefaultPortLayouts(IEnumerable<string>? portIds = null)
    {
        var normalizedPortIds = NormalizePortIds(portIds);
        return normalizedPortIds
            .Select(portId => new ReplayLogPortLayout(portId, DefaultLegacyActionIds))
            .ToArray();
    }

    public static IReadOnlyList<ReplayLogPortLayout> CreatePortLayouts(
        IEnumerable<string>? portIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? actionIdsByPort)
    {
        if (actionIdsByPort is null || actionIdsByPort.Count == 0)
            return CreateDefaultPortLayouts(portIds);

        var layouts = new List<ReplayLogPortLayout>(actionIdsByPort.Count);
        var seenPortIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var portId in NormalizePortIds(portIds).Concat(actionIdsByPort.Keys))
        {
            if (!seenPortIds.Add(portId))
                continue;

            if (!actionIdsByPort.TryGetValue(portId, out var actionIds) || actionIds.Count == 0)
            {
                layouts.Add(new ReplayLogPortLayout(portId, DefaultLegacyActionIds));
                continue;
            }

            var normalizedActionIds = actionIds
                .Where(static actionId => !string.IsNullOrWhiteSpace(actionId))
                .Select(static actionId => actionId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            layouts.Add(new ReplayLogPortLayout(
                portId,
                normalizedActionIds.Length == 0 ? DefaultLegacyActionIds : normalizedActionIds));
        }

        return layouts.Count == 0
            ? CreateDefaultPortLayouts(portIds)
            : layouts;
    }

    public static IReadOnlySet<string> DecodeLegacyMask(byte mask)
    {
        var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var bitIndex = 0; bitIndex < DefaultLegacyActionIds.Count; bitIndex++)
        {
            if ((mask & (1 << bitIndex)) != 0)
                actions.Add(DefaultLegacyActionIds[bitIndex]);
        }

        return actions;
    }

    private static IReadOnlyList<string> NormalizePortIds(IEnumerable<string>? portIds)
    {
        var normalized = (portIds ?? ["p1", "p2"])
            .Where(static portId => !string.IsNullOrWhiteSpace(portId))
            .Select(static portId => portId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? ["p1", "p2"] : normalized;
    }
}

internal sealed class ReplayLogPortLayout
{
    private readonly IReadOnlyDictionary<string, int> _bitIndexByActionId;

    public ReplayLogPortLayout(string portId, IReadOnlyList<string> actionIds)
    {
        PortId = string.IsNullOrWhiteSpace(portId)
            ? throw new ArgumentException("Port id is required.", nameof(portId))
            : portId.Trim();
        ActionIds = actionIds
            .Where(static actionId => !string.IsNullOrWhiteSpace(actionId))
            .Select(static actionId => actionId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ActionIds.Count == 0)
            throw new ArgumentException("At least one action id is required.", nameof(actionIds));

        _bitIndexByActionId = ActionIds
            .Select((actionId, index) => new KeyValuePair<string, int>(actionId, index))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public string PortId { get; }

    public IReadOnlyList<string> ActionIds { get; }

    public int ByteLength => (ActionIds.Count + 7) / 8;

    public bool TryGetBitIndex(string actionId, out int bitIndex) =>
        _bitIndexByActionId.TryGetValue(actionId, out bitIndex);
}
