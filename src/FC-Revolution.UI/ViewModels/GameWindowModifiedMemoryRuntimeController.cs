using System;
using System.Collections.Generic;
using System.IO;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowModifiedMemoryRuntimeController
{
    private readonly Action<GameWindowModifiedMemoryAutoApplyDecision> _applySavedMemoryDecision;
    private readonly Action<GameWindowModifiedMemoryLockUpsertDecision> _upsertModifiedMemoryEntry;
    private readonly Action<GameWindowModifiedMemoryLockRemoveDecision> _removeModifiedMemoryEntry;
    private readonly Action<GameWindowModifiedMemoryLockReplaceDecision> _replaceModifiedMemoryEntries;
    private readonly string _romPath;
    private readonly Action<string> _setStatus;

    public GameWindowModifiedMemoryRuntimeController(
        Action<GameWindowModifiedMemoryAutoApplyDecision> applySavedMemoryDecision,
        Action<GameWindowModifiedMemoryLockUpsertDecision> upsertModifiedMemoryEntry,
        Action<GameWindowModifiedMemoryLockRemoveDecision> removeModifiedMemoryEntry,
        Action<GameWindowModifiedMemoryLockReplaceDecision> replaceModifiedMemoryEntries,
        string romPath,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(applySavedMemoryDecision);
        ArgumentNullException.ThrowIfNull(upsertModifiedMemoryEntry);
        ArgumentNullException.ThrowIfNull(removeModifiedMemoryEntry);
        ArgumentNullException.ThrowIfNull(replaceModifiedMemoryEntries);
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
        ArgumentNullException.ThrowIfNull(setStatus);

        _applySavedMemoryDecision = applySavedMemoryDecision;
        _upsertModifiedMemoryEntry = upsertModifiedMemoryEntry;
        _removeModifiedMemoryEntry = removeModifiedMemoryEntry;
        _replaceModifiedMemoryEntries = replaceModifiedMemoryEntries;
        _romPath = romPath;
        _setStatus = setStatus;
    }

    public void ApplySavedProfile(RomConfigLoadResult loadResult)
    {
        ArgumentNullException.ThrowIfNull(loadResult);

        var applyDecision = GameWindowModifiedMemoryLockStateController.BuildAutoApplyDecision(loadResult.Profile);
        _applySavedMemoryDecision(applyDecision);

        if (loadResult.IsForeignMachineProfile)
            _setStatus($"运行中: {Path.GetFileName(_romPath)} | 警告：当前 .fcr 来自其他设备");
        else if (loadResult.IsFutureVersionProfile)
            _setStatus($"运行中: {Path.GetFileName(_romPath)} | 警告：当前 .fcr 版本高于程序支持");
    }

    public void UpsertRuntimeEntry(ModifiedMemoryRuntimeEntry entry)
    {
        var decision = GameWindowModifiedMemoryLockStateController.BuildUpsertDecision(entry);
        _upsertModifiedMemoryEntry(decision);
    }

    public void RemoveRuntimeEntry(ushort address)
    {
        var decision = GameWindowModifiedMemoryLockStateController.BuildRemoveDecision(address);
        _removeModifiedMemoryEntry(decision);
    }

    public void ReplaceRuntimeEntries(IReadOnlyList<ModifiedMemoryRuntimeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var decision = GameWindowModifiedMemoryLockStateController.BuildReplaceDecision(entries);
        _replaceModifiedMemoryEntries(decision);
    }
}
