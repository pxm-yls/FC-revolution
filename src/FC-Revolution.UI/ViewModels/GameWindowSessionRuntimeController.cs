using System;
using System.Collections.Generic;
using FCRevolution.Core.Debug;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowSessionTimelinePosition(
    long CurrentFrame,
    long NewestFrame,
    double TimestampSeconds);

internal sealed class GameWindowSessionRuntimeController
{
    private readonly object _syncRoot;
    private readonly IEmulatorCoreSession _coreSession;
    private readonly ITimeTravelService _timeTravelService;
    private readonly ICoreInputStateWriter _inputStateWriter;
    private readonly Dictionary<ushort, byte> _lockedMemoryValues = [];
    private volatile bool _isPaused;

    public GameWindowSessionRuntimeController(
        object syncRoot,
        IEmulatorCoreSession coreSession,
        ICoreDebugSurface debugSurface,
        ITimeTravelService timeTravelService,
        ICoreInputStateWriter inputStateWriter)
    {
        ArgumentNullException.ThrowIfNull(syncRoot);
        ArgumentNullException.ThrowIfNull(coreSession);
        ArgumentNullException.ThrowIfNull(debugSurface);
        ArgumentNullException.ThrowIfNull(timeTravelService);
        ArgumentNullException.ThrowIfNull(inputStateWriter);

        _syncRoot = syncRoot;
        _coreSession = coreSession;
        _timeTravelService = timeTravelService;
        _inputStateWriter = inputStateWriter;
        DebugSurface = debugSurface;
    }

    public ICoreDebugSurface DebugSurface { get; }

    public bool IsPaused => _isPaused;

    public int SnapshotInterval
    {
        get
        {
            lock (_syncRoot)
            {
                return _timeTravelService.SnapshotInterval;
            }
        }
    }

    public CoreLoadResult LoadRom(string romPath)
    {
        lock (_syncRoot)
        {
            var loadResult = _coreSession.LoadMedia(new CoreMediaLoadRequest(romPath));
            if (loadResult.Success)
                _isPaused = false;

            return loadResult;
        }
    }

    public CoreStepResult RunFrame()
    {
        lock (_syncRoot)
        {
            ApplyLockedMemoryValues();
            var stepResult = _coreSession.RunFrame();
            if (stepResult.Success)
                ApplyLockedMemoryValues();

            return stepResult;
        }
    }

    public byte ReadMemory(ushort address)
    {
        lock (_syncRoot)
        {
            return DebugSurface.ReadMemory(address);
        }
    }

    public void WriteMemory(ushort address, byte value)
    {
        lock (_syncRoot)
        {
            DebugSurface.WriteMemory(address, value);
        }
    }

    public CoreDebugState CaptureDebugState()
    {
        lock (_syncRoot)
        {
            return DebugSurface.CaptureDebugState();
        }
    }

    public DebugRefreshSnapshot CaptureDebugRefreshSnapshot(DebugRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_syncRoot)
        {
            if (!request.RequiresAnyCapture)
                return new DebugRefreshSnapshot();

            var state = request.RequiresState ? DebugSurface.CaptureDebugState() : new CoreDebugState();
            var memoryStart = (ushort)(request.MemoryPageIndex * DebugViewModel.MemoryPageSize);
            var stackStart = (ushort)(0x0100 + request.StackPageIndex * DebugViewModel.StackPageSize);
            var zeroPageStart = (ushort)(request.ZeroPageSliceIndex * DebugViewModel.ZeroPageSliceSize);
            var disasmStart = unchecked((ushort)(state.InstructionPointer + request.DisasmPageIndex * DebugViewModel.DisasmPageSize));

            return new DebugRefreshSnapshot
            {
                State = state,
                MemoryPageStart = memoryStart,
                MemoryPage = request.CaptureMemoryPage ? DebugSurface.ReadMemoryBlock(memoryStart, DebugViewModel.MemoryPageSize) : [],
                StackPageStart = stackStart,
                StackPage = request.CaptureStack ? DebugSurface.ReadMemoryBlock(stackStart, DebugViewModel.StackPageSize) : [],
                ZeroPageStart = zeroPageStart,
                ZeroPage = request.CaptureZeroPage ? DebugSurface.ReadMemoryBlock(zeroPageStart, DebugViewModel.ZeroPageSliceSize) : [],
                DisasmStart = disasmStart,
                Disasm = request.CaptureDisasm ? DebugSurface.ReadMemoryBlock(disasmStart, DebugViewModel.DisasmPageSize) : []
            };
        }
    }

    public byte[] ReadMemoryBlock(ushort startAddress, int length) =>
        RunLocked(() => DebugSurface.ReadMemoryBlock(startAddress, length));

    public void PauseForFailure()
    {
        lock (_syncRoot)
        {
            _coreSession.Pause();
            _isPaused = true;
            _lockedMemoryValues.Clear();
        }
    }

    public CoreStateBlob CaptureState()
    {
        lock (_syncRoot)
        {
            return _coreSession.CaptureState();
        }
    }

    public void RestoreState(CoreStateBlob state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_syncRoot)
        {
            _coreSession.RestoreState(state);
        }
    }

    public bool TogglePause()
    {
        lock (_syncRoot)
        {
            if (!_isPaused)
            {
                _coreSession.Pause();
                _isPaused = true;
            }
            else
            {
                _coreSession.Resume();
                _isPaused = false;
            }

            return _isPaused;
        }
    }

    public void ApplySavedMemoryDecision(GameWindowModifiedMemoryAutoApplyDecision decision)
    {
        lock (_syncRoot)
        {
            _lockedMemoryValues.Clear();
            if (!decision.ShouldApply)
                return;

            foreach (var entry in decision.RuntimeEntries)
                DebugSurface.WriteMemory(entry.Address, entry.Value);

            foreach (var lockedEntry in decision.LockedEntries)
                _lockedMemoryValues[lockedEntry.Address] = lockedEntry.Value;
        }
    }

    public void UpsertModifiedMemoryEntry(GameWindowModifiedMemoryLockUpsertDecision decision)
    {
        lock (_syncRoot)
        {
            if (decision.Action == GameWindowModifiedMemoryLockUpsertAction.Upsert)
            {
                _lockedMemoryValues[decision.Address] = decision.Value;
                if (decision.ShouldWriteValueImmediately)
                    DebugSurface.WriteMemory(decision.Address, decision.Value);

                return;
            }

            _lockedMemoryValues.Remove(decision.Address);
        }
    }

    public void RemoveModifiedMemoryEntry(GameWindowModifiedMemoryLockRemoveDecision decision)
    {
        lock (_syncRoot)
        {
            _lockedMemoryValues.Remove(decision.Address);
        }
    }

    public void ReplaceModifiedMemoryEntries(GameWindowModifiedMemoryLockReplaceDecision decision)
    {
        lock (_syncRoot)
        {
            _lockedMemoryValues.Clear();
            foreach (var entry in decision.LockedEntries)
            {
                _lockedMemoryValues[entry.Address] = entry.Value;
                DebugSurface.WriteMemory(entry.Address, entry.Value);
            }
        }
    }

    public void SetInputState(string portId, string actionId, float value)
    {
        lock (_syncRoot)
        {
            _inputStateWriter.SetInputState(portId, actionId, value);
        }
    }

    public GameWindowSessionTimelinePosition CaptureTimelinePosition()
    {
        lock (_syncRoot)
        {
            return new GameWindowSessionTimelinePosition(
                _timeTravelService.CurrentFrame,
                _timeTravelService.NewestFrame,
                _timeTravelService.CurrentTimestampSeconds);
        }
    }

    public CoreTimelineSnapshot? GetNearestSnapshot(long frame)
    {
        lock (_syncRoot)
        {
            return _timeTravelService.GetNearestSnapshot(frame);
        }
    }

    public long SeekToFrame(long frame)
    {
        lock (_syncRoot)
        {
            return _timeTravelService.SeekToFrame(frame);
        }
    }

    public void PauseRecording()
    {
        lock (_syncRoot)
        {
            _timeTravelService.PauseRecording();
        }
    }

    public void ResumeRecording()
    {
        lock (_syncRoot)
        {
            _timeTravelService.ResumeRecording();
        }
    }

    private void ApplyLockedMemoryValues()
    {
        if (_lockedMemoryValues.Count == 0)
            return;

        foreach (var (address, value) in _lockedMemoryValues)
            DebugSurface.WriteMemory(address, value);
    }

    private T RunLocked<T>(Func<T> action)
    {
        lock (_syncRoot)
        {
            return action();
        }
    }
}
