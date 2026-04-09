namespace FC_Revolution.UI.Models;

public readonly record struct ModifiedMemoryRuntimeEntry(ushort Address, byte Value, bool IsLocked);
