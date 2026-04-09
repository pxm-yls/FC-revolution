using System;
using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowDebugViewModelFactory
{
    private readonly string _displayName;
    private readonly string _romPath;
    private readonly GameWindowSessionRuntimeController _sessionRuntime;
    private readonly Action<ModifiedMemoryRuntimeEntry> _upsertModifiedMemoryRuntimeEntry;
    private readonly Action<ushort> _removeModifiedMemoryRuntimeEntry;
    private readonly Action<IReadOnlyList<ModifiedMemoryRuntimeEntry>> _replaceModifiedMemoryRuntimeEntries;
    private readonly Action<string> _notifySessionFailure;

    public GameWindowDebugViewModelFactory(
        string displayName,
        string romPath,
        GameWindowSessionRuntimeController sessionRuntime,
        Action<ModifiedMemoryRuntimeEntry> upsertModifiedMemoryRuntimeEntry,
        Action<ushort> removeModifiedMemoryRuntimeEntry,
        Action<IReadOnlyList<ModifiedMemoryRuntimeEntry>> replaceModifiedMemoryRuntimeEntries,
        Action<string> notifySessionFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
        ArgumentNullException.ThrowIfNull(sessionRuntime);
        ArgumentNullException.ThrowIfNull(upsertModifiedMemoryRuntimeEntry);
        ArgumentNullException.ThrowIfNull(removeModifiedMemoryRuntimeEntry);
        ArgumentNullException.ThrowIfNull(replaceModifiedMemoryRuntimeEntries);
        ArgumentNullException.ThrowIfNull(notifySessionFailure);

        _displayName = displayName;
        _romPath = romPath;
        _sessionRuntime = sessionRuntime;
        _upsertModifiedMemoryRuntimeEntry = upsertModifiedMemoryRuntimeEntry;
        _removeModifiedMemoryRuntimeEntry = removeModifiedMemoryRuntimeEntry;
        _replaceModifiedMemoryRuntimeEntries = replaceModifiedMemoryRuntimeEntries;
        _notifySessionFailure = notifySessionFailure;
    }

    public DebugViewModel Create(DebugWindowDisplaySettingsProfile activeDisplaySettings)
    {
        ArgumentNullException.ThrowIfNull(activeDisplaySettings);

        return new DebugViewModel(
            _displayName,
            _romPath,
            _sessionRuntime.DebugSurface,
            () => _sessionRuntime.CaptureDebugState(),
            address => _sessionRuntime.ReadMemory(address),
            (address, value) => _sessionRuntime.WriteMemory(address, value),
            request => _sessionRuntime.CaptureDebugRefreshSnapshot(request),
            _upsertModifiedMemoryRuntimeEntry,
            _removeModifiedMemoryRuntimeEntry,
            _replaceModifiedMemoryRuntimeEntries,
            _notifySessionFailure,
            activeDisplaySettings.Clone());
    }
}
