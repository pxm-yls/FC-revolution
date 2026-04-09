using System;
using System.IO;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Metal;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowRomLoadHandler
{
    private readonly Action _resetTemporalHistory;
    private readonly Func<string, CoreLoadResult> _loadRom;
    private readonly Func<string> _describeMapper;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _writeDiagnostic;
    private readonly Action<MacMetalTemporalResetReason> _requestTemporalHistoryReset;

    public GameWindowRomLoadHandler(
        Action resetTemporalHistory,
        Func<string, CoreLoadResult> loadRom,
        Func<string> describeMapper,
        Action<string> setStatus,
        Action<string> writeDiagnostic,
        Action<MacMetalTemporalResetReason> requestTemporalHistoryReset)
    {
        ArgumentNullException.ThrowIfNull(resetTemporalHistory);
        ArgumentNullException.ThrowIfNull(loadRom);
        ArgumentNullException.ThrowIfNull(describeMapper);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(writeDiagnostic);
        ArgumentNullException.ThrowIfNull(requestTemporalHistoryReset);

        _resetTemporalHistory = resetTemporalHistory;
        _loadRom = loadRom;
        _describeMapper = describeMapper;
        _setStatus = setStatus;
        _writeDiagnostic = writeDiagnostic;
        _requestTemporalHistoryReset = requestTemporalHistoryReset;
    }

    public void Load(string romPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);

        _resetTemporalHistory();

        var loadResult = _loadRom(romPath);
        if (!loadResult.Success)
            throw new InvalidOperationException(loadResult.ErrorMessage ?? $"Failed to load ROM '{romPath}'.");

        var fileName = Path.GetFileName(romPath);
        var mapperDescription = _describeMapper();
        _setStatus($"运行中: {fileName} | 核心为 {mapperDescription}");
        _writeDiagnostic($"游戏窗口运行中 {fileName}，核心为 {mapperDescription}");
        _requestTemporalHistoryReset(MacMetalTemporalResetReason.RomLoaded);
    }
}
