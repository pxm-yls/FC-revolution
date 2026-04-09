namespace FCRevolution.Emulation.Abstractions;

public sealed class CoreDebugState
{
    public ushort InstructionPointer { get; init; }

    public string InstructionPointerLabel { get; init; } = "IP";

    public IReadOnlyList<CoreDebugSection> Sections { get; init; } = [];
}

public sealed record CoreDebugSection(
    string SectionId,
    string DisplayName,
    string Category,
    IReadOnlyList<CoreDebugValue> Values);

public sealed record CoreDebugValue(string Label, string Value);
