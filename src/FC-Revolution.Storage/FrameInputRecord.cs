namespace FCRevolution.Storage;

/// <summary>Compact per-frame input log for deterministic replay/export compatibility.</summary>
public readonly record struct FrameInputRecord(long Frame, IReadOnlyDictionary<string, IReadOnlySet<string>> ActionsByPort)
{
    private static readonly IReadOnlySet<string> EmptyActions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> GetPressedActions(string portId) =>
        ActionsByPort.TryGetValue(portId, out var actions) ? actions : EmptyActions;

    public bool IsActionPressed(string portId, string actionId) =>
        GetPressedActions(portId).Contains(actionId);
}
