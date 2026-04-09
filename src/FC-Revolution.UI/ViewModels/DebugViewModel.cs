using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class DebugViewModel : ViewModelBase, IDisposable
{
    internal const int MemoryPageSize = 0x80;
    private const int MemoryColumns = 0x10;
    internal const int StackPageSize = 0x40;
    internal const int ZeroPageSliceSize = 0x40;
    internal const int DisasmPageSize = 12;
    private const int CompactMemoryColumns = 8;
    private const int ModifiedMemoryPageSize = 10;

    private readonly Func<ushort, byte> _readMemory;
    private readonly Func<ushort, int, byte[]> _readMemoryBlock;
    private readonly Action<ushort, byte> _writeMemory;
    private readonly Func<CoreDebugState> _captureDebugState;
    private readonly bool _useBulkReadMemory;
    private readonly Func<DebugRefreshRequest, DebugRefreshSnapshot>? _captureRefreshSnapshot;
    private readonly Action<ModifiedMemoryRuntimeEntry>? _upsertModifiedMemoryRuntimeEntry;
    private readonly Action<ushort>? _removeModifiedMemoryRuntimeEntry;
    private readonly Action<IReadOnlyList<ModifiedMemoryRuntimeEntry>>? _replaceModifiedMemoryRuntimeEntries;
    private readonly Action<string>? _notifySessionFailure;
    private readonly DispatcherTimer _liveRefreshTimer;
    private readonly string _romPath;
    private readonly DebugWindowDisplaySettingsProfile _activeDisplaySettings;

    private string _editStatus = "就绪";
    private string _addressInput = "0000";
    private string _valueInput = "00";
    private string _memoryPageInput = "1";
    private int _memoryPageIndex;
    private int _stackPageIndex;
    private int _zeroPageSliceIndex;
    private int _disasmPageIndex;
    private int _modifiedMemoryPageIndex;
    private ushort? _highlightedMemoryAddress;
    private bool _isDisposed;
    private bool _isMemoryCellEditing;
    private bool _isApplyingDisplaySettings;
    private bool _hasSessionFailure;
    private bool _refreshScheduled;
    private bool _showRegisters;
    private bool _showPpu;
    private bool _showDisasm;
    private bool _showStack;
    private bool _showZeroPage;
    private bool _showMemoryEditor = true;
    private bool _showMemoryPage = true;
    private bool _showModifiedMemory = true;
    private bool _pendingShowRegisters;
    private bool _pendingShowPpu;
    private bool _pendingShowDisasm;
    private bool _pendingShowStack;
    private bool _pendingShowZeroPage;
    private bool _pendingShowMemoryEditor = true;
    private bool _pendingShowMemoryPage = true;
    private bool _pendingShowModifiedMemory = true;

    public DebugViewModel(
        string currentGameTitle,
        string romPath,
        ICoreDebugSurface debugSurface,
        Func<CoreDebugState>? captureDebugState = null,
        Func<ushort, byte>? readMemory = null,
        Action<ushort, byte>? writeMemory = null,
        Func<DebugRefreshRequest, DebugRefreshSnapshot>? captureRefreshSnapshot = null,
        Action<ModifiedMemoryRuntimeEntry>? upsertModifiedMemoryRuntimeEntry = null,
        Action<ushort>? removeModifiedMemoryRuntimeEntry = null,
        Action<IReadOnlyList<ModifiedMemoryRuntimeEntry>>? replaceModifiedMemoryRuntimeEntries = null,
        Action<string>? notifySessionFailure = null,
        DebugWindowDisplaySettingsProfile? activeDisplaySettings = null,
        bool enableLiveRefresh = true)
    {
        ArgumentNullException.ThrowIfNull(debugSurface);

        CurrentGameTitle = currentGameTitle;
        _romPath = romPath;
        _captureDebugState = captureDebugState ?? debugSurface.CaptureDebugState;
        _useBulkReadMemory = readMemory == null;
        _readMemory = readMemory ?? debugSurface.ReadMemory;
        _readMemoryBlock = debugSurface.ReadMemoryBlock;
        _writeMemory = writeMemory ?? debugSurface.WriteMemory;
        _captureRefreshSnapshot = captureRefreshSnapshot;
        _upsertModifiedMemoryRuntimeEntry = upsertModifiedMemoryRuntimeEntry;
        _removeModifiedMemoryRuntimeEntry = removeModifiedMemoryRuntimeEntry;
        _replaceModifiedMemoryRuntimeEntries = replaceModifiedMemoryRuntimeEntries;
        _notifySessionFailure = notifySessionFailure;
        _activeDisplaySettings = DebugWindowDisplaySettingsProfile.Sanitize(
            activeDisplaySettings ?? DebugDisplaySettingsController.LoadFromSystemConfig());
        _liveRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _liveRefreshTimer.Tick += OnLiveRefreshTick;

        for (var i = 0; i < MemoryColumns; i++)
            MemoryColumnHeaders.Add($"+{i:X1}");

        ApplyActiveDisplaySettings(_activeDisplaySettings);
        LoadProfile(replaceRuntimeLocks: false);
        LoadPendingDisplaySettingsFromSystemConfig();
        RefreshModifiedMemoryPage();

        if (enableLiveRefresh)
            _liveRefreshTimer.Start();
    }

    public string CurrentGameTitle { get; }

    private DebugLayoutViewState LayoutViewState =>
        DebugLayoutStateController.BuildLayoutViewState(CreateActiveDisplaySettingsProfile());

    private DebugPendingDisplaySettingsViewState PendingDisplaySettingsViewState =>
        DebugLayoutStateController.BuildPendingDisplaySettingsViewState(
            CreateActiveDisplaySettingsProfile(),
            CreatePendingDisplaySettingsProfile());

    public string WindowSummary => $"当前游戏：{CurrentGameTitle}";

    public string EditStatus
    {
        get => _editStatus;
        private set => SetProperty(ref _editStatus, value);
    }

    public string AddressInput
    {
        get => _addressInput;
        set => SetProperty(ref _addressInput, value);
    }

    public string ValueInput
    {
        get => _valueInput;
        set => SetProperty(ref _valueInput, value);
    }

    public string MemoryPageInput
    {
        get => _memoryPageInput;
        set => SetProperty(ref _memoryPageInput, value);
    }

    public bool ShowRegisters => _showRegisters;

    public bool ShowPpu => _showPpu;

    public bool ShowDisasm => _showDisasm;

    public bool ShowStack => _showStack;

    public bool ShowZeroPage => _showZeroPage;

    public bool ShowMemoryEditor => _showMemoryEditor;

    public bool ShowMemoryPage => _showMemoryPage;

    public bool ShowModifiedMemory => _showModifiedMemory;

    public bool PendingShowRegisters
    {
        get => _pendingShowRegisters;
        set => SetPendingSectionVisibility(ref _pendingShowRegisters, value);
    }

    public bool PendingShowPpu
    {
        get => _pendingShowPpu;
        set => SetPendingSectionVisibility(ref _pendingShowPpu, value);
    }

    public bool PendingShowDisasm
    {
        get => _pendingShowDisasm;
        set => SetPendingSectionVisibility(ref _pendingShowDisasm, value);
    }

    public bool PendingShowStack
    {
        get => _pendingShowStack;
        set => SetPendingSectionVisibility(ref _pendingShowStack, value);
    }

    public bool PendingShowZeroPage
    {
        get => _pendingShowZeroPage;
        set => SetPendingSectionVisibility(ref _pendingShowZeroPage, value);
    }

    public bool PendingShowMemoryEditor
    {
        get => _pendingShowMemoryEditor;
        set => SetPendingSectionVisibility(ref _pendingShowMemoryEditor, value);
    }

    public bool PendingShowMemoryPage
    {
        get => _pendingShowMemoryPage;
        set => SetPendingSectionVisibility(ref _pendingShowMemoryPage, value);
    }

    public bool PendingShowModifiedMemory
    {
        get => _pendingShowModifiedMemory;
        set => SetPendingSectionVisibility(ref _pendingShowModifiedMemory, value);
    }

    public ObservableCollection<string> MemoryColumnHeaders { get; } = new();
    public ObservableCollection<MemoryPageRow> RegisterRows { get; } = new();
    public ObservableCollection<MemoryPageRow> PpuRows { get; } = new();
    public ObservableCollection<MemoryPageRow> MemoryRows { get; } = new();
    public ObservableCollection<MemoryPageRow> StackRows { get; } = new();
    public ObservableCollection<MemoryPageRow> ZeroPageRows { get; } = new();
    public ObservableCollection<MemoryPageRow> DisasmRows { get; } = new();
    public ObservableCollection<ModifiedMemoryEntry> ModifiedMemoryEntries { get; } = new();
    public ObservableCollection<ModifiedMemoryEntry> VisibleModifiedMemoryEntries { get; } = new();

    public bool HasVisibleLeftSections => LayoutViewState.HasVisibleLeftSections;

    public bool HasVisibleRightSections => LayoutViewState.HasVisibleRightSections;

    public bool ShowLeftPane => LayoutViewState.ShowLeftPane;

    public bool ShowRightPane => LayoutViewState.ShowRightPane;

    public int LeftPaneColumnSpan => LayoutViewState.LeftPaneColumnSpan;

    public int RightPaneColumn => LayoutViewState.RightPaneColumn;

    public int RightPaneColumnSpan => LayoutViewState.RightPaneColumnSpan;

    public double PreferredWindowWidth => LayoutViewState.PreferredWindowWidth;

    public double PreferredWindowHeight => LayoutViewState.PreferredWindowHeight;

    public double PreferredMinWidth => LayoutViewState.PreferredMinWidth;

    public double PreferredMinHeight => LayoutViewState.PreferredMinHeight;

    public int StackGridColumns => CompactMemoryColumns;

    public int ZeroPageGridColumns => CompactMemoryColumns;

    public double MemoryCellFontSize => LayoutViewState.MemoryCellFontSize;

    public double CompactMemoryCellFontSize => LayoutViewState.CompactMemoryCellFontSize;

    public bool HasModifiedMemoryEntries => ModifiedMemoryEntries.Count > 0;

    public bool ShowModifiedMemoryEmptyState => !HasModifiedMemoryEntries;

    public bool HasPendingDisplaySettingsChanges => PendingDisplaySettingsViewState.HasPendingDisplaySettingsChanges;

    public string DisplaySettingsRestartHint => PendingDisplaySettingsViewState.DisplaySettingsRestartHint;

    public bool ShowModifiedMemoryPagination => ModifiedMemoryEntries.Count > ModifiedMemoryPageSize;

    public string ModifiedMemoryPageSummary => $"第 {_modifiedMemoryPageIndex + 1} / {ModifiedMemoryPageCount} 页";

    public bool CanMoveToPreviousModifiedMemoryPage => _modifiedMemoryPageIndex > 0;

    public bool CanMoveToNextModifiedMemoryPage => _modifiedMemoryPageIndex + 1 < ModifiedMemoryPageCount;

    private int ModifiedMemoryPageCount => DebugModifiedMemoryListController.GetPageCount(ModifiedMemoryEntries.Count, ModifiedMemoryPageSize);

    public bool AutoApplyModifiedMemoryOnLaunch
    {
        get => RomConfigProfile.LoadValidated(_romPath).Profile.AutoApplyModifiedMemoryOnLaunch;
        set
        {
            var profile = RomConfigProfile.LoadValidated(_romPath).Profile;
            if (profile.AutoApplyModifiedMemoryOnLaunch == value)
                return;

            profile.AutoApplyModifiedMemoryOnLaunch = value;
            RomConfigProfile.Save(_romPath, profile);
            OnPropertyChanged();
        }
    }

    public string MemoryPageSummary
        => DebugPageStateController.BuildMemoryPageSummary(_memoryPageIndex, MemoryPageSize);

    public string MemoryPageNumber =>
        DebugPageStateController.BuildMemoryPageNumber(_memoryPageIndex, 0x10000 / MemoryPageSize);

    public string StackPageSummary
        => DebugPageStateController.BuildStackPageSummary(_stackPageIndex, StackPageSize);

    public string ZeroPageSummary
        => DebugPageStateController.BuildZeroPageSummary(_zeroPageSliceIndex, ZeroPageSliceSize);

    public string DisasmSummary => DebugPageStateController.BuildDisasmSummary(_disasmPageIndex);

    public void Refresh()
    {
        if (_isDisposed || _hasSessionFailure)
            return;

        try
        {
            ApplyRefreshSnapshot(CreateRefreshSnapshot());
        }
        catch (Exception ex)
        {
            HandleDebugFailure("实时调试刷新失败，当前游戏状态可能已损坏，请重新尝试。", ex);
        }
    }

    [RelayCommand]
    private void ReadMemory()
    {
        if (_hasSessionFailure)
        {
            EditStatus = "当前游戏会话已终止，请重新启动游戏后再试";
            return;
        }

        if (!DebugMemoryInputController.TryParseAddress(AddressInput, out var address))
        {
            EditStatus = DebugMemoryInputController.InvalidAddressMessage;
            return;
        }

        if (!TryReadMemory(address, out var value))
            return;

        var readSuccessState = DebugMemoryReadController.BuildReadSuccessState(address, value, MemoryPageSize);
        ValueInput = readSuccessState.ValueInput;
        _memoryPageIndex = readSuccessState.MemoryPageIndex;
        MemoryPageInput = readSuccessState.MemoryPageInput;
        _highlightedMemoryAddress = readSuccessState.HighlightedAddress;
        RaisePageProperties();
        EditStatus = readSuccessState.EditStatus;
        Refresh();
    }

    [RelayCommand]
    private void WriteMemory()
    {
        if (!DebugMemoryInputController.TryParseAddress(AddressInput, out var address))
        {
            EditStatus = DebugMemoryInputController.InvalidAddressMessage;
            return;
        }

        if (!DebugMemoryInputController.TryParseByte(ValueInput, out var value))
        {
            EditStatus = DebugMemoryInputController.InvalidByteMessage;
            return;
        }

        ApplyMemoryWrite(address, value);
    }

    [RelayCommand]
    private void PreviousMemoryPage()
    {
        var decision = DebugPageNavigationController.BuildMemoryPageMoveDecision(
            _memoryPageIndex,
            delta: -1,
            totalPages: 0x10000 / MemoryPageSize);
        if (!decision.Changed)
            return;

        _memoryPageIndex = decision.NextIndex;
        MemoryPageInput = decision.MemoryPageInput;
        RaisePageProperties();
        Refresh();
    }

    [RelayCommand]
    private void NextMemoryPage()
    {
        var decision = DebugPageNavigationController.BuildMemoryPageMoveDecision(
            _memoryPageIndex,
            delta: 1,
            totalPages: 0x10000 / MemoryPageSize);
        if (!decision.Changed)
            return;

        _memoryPageIndex = decision.NextIndex;
        MemoryPageInput = decision.MemoryPageInput;
        RaisePageProperties();
        Refresh();
    }

    [RelayCommand]
    private void JumpMemoryPage()
    {
        if (!DebugMemoryInputController.TryParseJumpPage(
                MemoryPageInput,
                0x10000 / MemoryPageSize,
                out var jumpPage))
        {
            EditStatus = DebugMemoryInputController.InvalidJumpPageMessage;
            return;
        }

        var decision = DebugPageNavigationController.BuildMemoryPageJumpDecision(jumpPage.PageNumber);
        _memoryPageIndex = decision.NextIndex;
        MemoryPageInput = decision.MemoryPageInput;
        RaisePageProperties();
        Refresh();
    }

    [RelayCommand]
    private void PreviousStackPage()
    {
        var decision = DebugPageNavigationController.BuildBoundedPageMoveDecision(_stackPageIndex, -1, 0, 3);
        if (!decision.Changed)
            return;

        _stackPageIndex = decision.NextIndex;
        OnPropertyChanged(nameof(StackPageSummary));
        Refresh();
    }

    [RelayCommand]
    private void NextStackPage()
    {
        var decision = DebugPageNavigationController.BuildBoundedPageMoveDecision(_stackPageIndex, 1, 0, 3);
        if (!decision.Changed)
            return;

        _stackPageIndex = decision.NextIndex;
        OnPropertyChanged(nameof(StackPageSummary));
        Refresh();
    }

    [RelayCommand]
    private void PreviousZeroPage()
    {
        var decision = DebugPageNavigationController.BuildBoundedPageMoveDecision(_zeroPageSliceIndex, -1, 0, 3);
        if (!decision.Changed)
            return;

        _zeroPageSliceIndex = decision.NextIndex;
        OnPropertyChanged(nameof(ZeroPageSummary));
        Refresh();
    }

    [RelayCommand]
    private void NextZeroPage()
    {
        var decision = DebugPageNavigationController.BuildBoundedPageMoveDecision(_zeroPageSliceIndex, 1, 0, 3);
        if (!decision.Changed)
            return;

        _zeroPageSliceIndex = decision.NextIndex;
        OnPropertyChanged(nameof(ZeroPageSummary));
        Refresh();
    }

    [RelayCommand]
    private void PreviousDisasmPage()
    {
        var decision = DebugPageNavigationController.BuildDisasmPageMoveDecision(_disasmPageIndex, delta: -1);
        _disasmPageIndex = decision.NextIndex;
        OnPropertyChanged(nameof(DisasmSummary));
        Refresh();
    }

    [RelayCommand]
    private void NextDisasmPage()
    {
        var decision = DebugPageNavigationController.BuildDisasmPageMoveDecision(_disasmPageIndex, delta: 1);
        _disasmPageIndex = decision.NextIndex;
        OnPropertyChanged(nameof(DisasmSummary));
        Refresh();
    }

    [RelayCommand]
    private void PreviousModifiedMemoryPage()
    {
        var decision = DebugPageNavigationController.BuildModifiedMemoryPageMoveDecision(
            _modifiedMemoryPageIndex,
            delta: -1,
            pageCount: ModifiedMemoryPageCount);
        if (!decision.Changed)
            return;

        _modifiedMemoryPageIndex = decision.NextIndex;
        RefreshModifiedMemoryPage();
    }

    [RelayCommand]
    private void NextModifiedMemoryPage()
    {
        var decision = DebugPageNavigationController.BuildModifiedMemoryPageMoveDecision(
            _modifiedMemoryPageIndex,
            delta: 1,
            pageCount: ModifiedMemoryPageCount);
        if (!decision.Changed)
            return;

        _modifiedMemoryPageIndex = decision.NextIndex;
        RefreshModifiedMemoryPage();
    }

    [RelayCommand]
    private void SelectMemoryCell(MemoryCellItem? cell)
    {
        if (cell == null)
            return;

        AddressInput = cell.Address.ToString("X4");
        ValueInput = cell.Value;
        _highlightedMemoryAddress = cell.Address;
        UpdateMemoryLocatorHighlights();
        EditStatus = $"已选中 ${cell.Address:X4}";
    }

    [RelayCommand]
    private void ApplySavedMemoryEntry(ModifiedMemoryEntry? entry)
    {
        if (entry == null || !DebugMemoryInputController.TryParseByte(entry.Value, out var value))
            return;

        ApplyMemoryWrite(entry.Address, value);
    }

    [RelayCommand]
    private void RemoveSavedMemoryEntry(ModifiedMemoryEntry? entry)
    {
        if (entry == null)
            return;

        var removeDecision = DebugMemoryWriteController.BuildRemoveDecision(entry);
        DebugModifiedMemoryListController.RemoveEntry(ModifiedMemoryEntries, entry);

        RefreshModifiedMemoryPage();
        SaveProfile();
        _removeModifiedMemoryRuntimeEntry?.Invoke(removeDecision.RemovedAddress);
        EditStatus = removeDecision.EditStatus;
    }

    [RelayCommand]
    private void ToggleSavedMemoryLock(ModifiedMemoryEntry? entry)
    {
        if (entry == null || !DebugMemoryInputController.TryParseByte(entry.Value, out var value))
            return;

        var toggleDecision = DebugMemoryWriteController.BuildToggleLockDecision(entry, value);
        try
        {
            entry.IsLocked = toggleDecision.NextIsLocked;
            SaveProfile();
            _upsertModifiedMemoryRuntimeEntry?.Invoke(toggleDecision.RuntimeEntry);
            EditStatus = toggleDecision.EditStatus;
        }
        catch (Exception ex)
        {
            entry.IsLocked = !toggleDecision.NextIsLocked;
            HandleDebugFailure($"锁定内存 ${entry.Address:X4} 失败，当前游戏状态可能已损坏，请重新尝试。", ex);
        }
    }

    [RelayCommand]
    private void ExportProfile()
    {
        SaveProfile();
        EditStatus = $"已导出 {Path.GetFileName(RomConfigProfile.GetProfilePath(_romPath))}";
    }

    public async Task ImportProfileAsync(Avalonia.Controls.Window owner)
    {
        var loadResult = RomConfigProfile.LoadValidated(_romPath);
        if (loadResult.HasProfileKindMismatch)
        {
            EditStatus = "该文件不是 FC-Revolution 专用 .fcr 配置";
            return;
        }

        if (loadResult.IsForeignMachineProfile)
        {
            var confirmed = await ProfileTrustDialog.ShowAsync(
                owner,
                "检测到外部 .fcr",
                "该 .fcr 来自其他设备。继续信任后，会将它重签名为当前设备，后续不再重复提示。");

            if (!confirmed)
            {
                EditStatus = "已取消导入外部 .fcr";
                return;
            }

            RomConfigProfile.TrustCurrentMachine(_romPath);
            loadResult = RomConfigProfile.LoadValidated(_romPath);
        }

        LoadProfile(replaceRuntimeLocks: true);
        Refresh();
        EditStatus = DebugModifiedMemoryProfileController.BuildProfileStatus(
            $"已导入 {Path.GetFileName(RomConfigProfile.GetProfilePath(_romPath))}",
            loadResult);
    }

    public void CommitMemoryCellEdit(MemoryCellItem? cell)
    {
        if (cell == null || !DebugMemoryInputController.TryParseByte(cell.Value, out var value))
        {
            if (cell != null)
            {
                if (TryReadMemory(cell.Address, out var currentValue))
                    cell.Value = currentValue.ToString("X2");
            }
            return;
        }

        ApplyMemoryWrite(cell.Address, value, deferRefresh: true);
    }

    public void SetMemoryCellEditing(bool isEditing)
    {
        _isMemoryCellEditing = isEditing;
    }

    private bool SetPendingSectionVisibility(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
            return false;

        RaiseDisplaySettingsStatusProperties();
        SavePendingDisplaySettingsToSystemConfig();
        EditStatus = "显示设置已保存，重启当前游戏后生效";
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _liveRefreshTimer.Stop();
        _liveRefreshTimer.Tick -= OnLiveRefreshTick;
    }

    private void OnLiveRefreshTick(object? sender, EventArgs e)
    {
        if (!DebugLiveRefreshOrchestrator.ShouldRunLiveRefreshTick(
                _isDisposed,
                _hasSessionFailure,
                _isMemoryCellEditing,
                HasActiveRefreshSections()))
            return;

        Refresh();
    }

    private DebugRefreshSnapshot CreateRefreshSnapshot()
    {
        var request = CreateRefreshRequest();
        if (_captureRefreshSnapshot != null)
            return _captureRefreshSnapshot(request);

        var capturePlan = DebugLiveRefreshOrchestrator.BuildCapturePlan(request);
        if (!capturePlan.RequiresAnyCapture)
            return new DebugRefreshSnapshot();

        var state = capturePlan.RequiresState ? _captureDebugState() : new CoreDebugState();
        var addressPlan = DebugLiveRefreshOrchestrator.BuildAddressPlan(
            request,
            state.InstructionPointer,
            MemoryPageSize,
            StackPageSize,
            ZeroPageSliceSize,
            DisasmPageSize);

        return new DebugRefreshSnapshot
        {
            State = state,
            MemoryPageStart = addressPlan.MemoryPageStart,
            MemoryPage = request.CaptureMemoryPage ? ReadMemoryBlock(addressPlan.MemoryPageStart, MemoryPageSize) : [],
            StackPageStart = addressPlan.StackPageStart,
            StackPage = request.CaptureStack ? ReadMemoryBlock(addressPlan.StackPageStart, StackPageSize) : [],
            ZeroPageStart = addressPlan.ZeroPageStart,
            ZeroPage = request.CaptureZeroPage ? ReadMemoryBlock(addressPlan.ZeroPageStart, ZeroPageSliceSize) : [],
            DisasmStart = addressPlan.DisasmStart,
            Disasm = request.CaptureDisasm ? ReadMemoryBlock(addressPlan.DisasmStart, DisasmPageSize) : []
        };
    }

    private void ApplyRefreshSnapshot(DebugRefreshSnapshot snapshot)
    {
        var applyPlan = DebugLiveRefreshOrchestrator.BuildApplyPlan(
            ShowRegisters,
            ShowPpu,
            ShowMemoryPage,
            ShowStack,
            ShowZeroPage,
            ShowDisasm);

        if (applyPlan.ApplyRegisters)
            DebugStateRowsBuilder.BuildRegisterRows(RegisterRows, snapshot.State);

        if (applyPlan.ApplyPpu)
            DebugStateRowsBuilder.BuildPpuRows(PpuRows, snapshot.State);

        if (applyPlan.ApplyMemoryPage)
            DebugMemoryGridBuilder.BuildMemoryRows(MemoryRows, snapshot.MemoryPageStart, snapshot.MemoryPage, rowHeaderOffset: true, columns: MemoryColumns, highlightAddress: _highlightedMemoryAddress);

        if (applyPlan.ApplyStack)
            DebugMemoryGridBuilder.BuildMemoryRows(StackRows, snapshot.StackPageStart, snapshot.StackPage, rowHeaderOffset: true, columns: StackGridColumns);

        if (applyPlan.ApplyZeroPage)
            DebugMemoryGridBuilder.BuildMemoryRows(ZeroPageRows, snapshot.ZeroPageStart, snapshot.ZeroPage, rowHeaderOffset: true, columns: ZeroPageGridColumns);

        if (applyPlan.ApplyDisasm)
            DebugStateRowsBuilder.BuildDisasmRows(DisasmRows, snapshot.State, snapshot.DisasmStart, snapshot.Disasm);
    }

    private byte[] ReadMemoryBlock(ushort startAddress, int length)
    {
        if (_useBulkReadMemory)
            return _readMemoryBlock(startAddress, length);

        var values = new byte[length];
        for (var index = 0; index < length; index++)
            values[index] = _readMemory(unchecked((ushort)(startAddress + index)));

        return values;
    }

    private void ApplyMemoryWrite(ushort address, byte value, bool deferRefresh = false)
    {
        if (_hasSessionFailure)
        {
            EditStatus = "当前游戏会话已终止，请重新启动游戏后再试";
            return;
        }

        try
        {
            _writeMemory(address, value);
            TrackModifiedEntry(address, value);
            var writeSuccessState = DebugMemoryWriteController.BuildWriteSuccessState(address, value, MemoryPageSize);
            _memoryPageIndex = writeSuccessState.MemoryPageIndex;
            MemoryPageInput = writeSuccessState.MemoryPageInput;
            _highlightedMemoryAddress = writeSuccessState.HighlightedAddress;
            RaisePageProperties();
            EditStatus = writeSuccessState.EditStatus;

            if (deferRefresh)
                ScheduleRefresh();
            else
                Refresh();
        }
        catch (Exception ex)
        {
            HandleDebugFailure($"你修改的内存值 ${address:X4} = ${value:X2} 可能导致当前游戏崩溃，请重新尝试。", ex);
        }
    }

    private void RaisePageProperties()
    {
        OnPropertyChanged(nameof(MemoryPageSummary));
        OnPropertyChanged(nameof(MemoryPageNumber));
    }

    private void RaiseLayoutProperties()
    {
        OnPropertyChanged(nameof(HasVisibleLeftSections));
        OnPropertyChanged(nameof(HasVisibleRightSections));
        OnPropertyChanged(nameof(ShowLeftPane));
        OnPropertyChanged(nameof(ShowRightPane));
        OnPropertyChanged(nameof(LeftPaneColumnSpan));
        OnPropertyChanged(nameof(RightPaneColumn));
        OnPropertyChanged(nameof(RightPaneColumnSpan));
        OnPropertyChanged(nameof(PreferredWindowWidth));
        OnPropertyChanged(nameof(PreferredWindowHeight));
        OnPropertyChanged(nameof(PreferredMinWidth));
        OnPropertyChanged(nameof(PreferredMinHeight));
        OnPropertyChanged(nameof(StackGridColumns));
        OnPropertyChanged(nameof(ZeroPageGridColumns));
        OnPropertyChanged(nameof(MemoryCellFontSize));
        OnPropertyChanged(nameof(CompactMemoryCellFontSize));
    }

    private void RefreshModifiedMemoryPage()
    {
        var pageState = DebugModifiedMemoryListController.BuildPageState(
            ModifiedMemoryEntries,
            _modifiedMemoryPageIndex,
            ModifiedMemoryPageSize);
        _modifiedMemoryPageIndex = pageState.PageIndex;

        VisibleModifiedMemoryEntries.Clear();
        foreach (var entry in pageState.VisibleEntries)
        {
            VisibleModifiedMemoryEntries.Add(entry);
        }

        OnPropertyChanged(nameof(HasModifiedMemoryEntries));
        OnPropertyChanged(nameof(ShowModifiedMemoryEmptyState));
        OnPropertyChanged(nameof(ShowModifiedMemoryPagination));
        OnPropertyChanged(nameof(ModifiedMemoryPageSummary));
        OnPropertyChanged(nameof(CanMoveToPreviousModifiedMemoryPage));
        OnPropertyChanged(nameof(CanMoveToNextModifiedMemoryPage));
    }

    private void TrackModifiedEntry(ushort address, byte value)
    {
        var trackingResult = DebugMemoryWriteController.TrackModifiedEntry(ModifiedMemoryEntries, address, value);
        _modifiedMemoryPageIndex = trackingResult.NextModifiedMemoryPageIndex;
        RefreshModifiedMemoryPage();
        SaveProfile();
        _upsertModifiedMemoryRuntimeEntry?.Invoke(trackingResult.RuntimeEntry);
    }

    private void LoadProfile(bool replaceRuntimeLocks)
    {
        if (!DebugModifiedMemoryProfileController.TryLoad(_romPath, out var profileLoad))
            return;

        DebugModifiedMemoryListController.ReplaceEntries(ModifiedMemoryEntries, profileLoad.Entries);

        RefreshModifiedMemoryPage();
        if (replaceRuntimeLocks)
            ReplaceRuntimeModifiedMemoryEntries();
        EditStatus = DebugModifiedMemoryProfileController.BuildProfileStatus("已载入 .fcr 配置", profileLoad.LoadResult);
    }

    private void SaveProfile()
    {
        DebugModifiedMemoryProfileController.Save(_romPath, ModifiedMemoryEntries);
    }

    private void ApplyActiveDisplaySettings(DebugWindowDisplaySettingsProfile settings)
    {
        _showRegisters = settings.ShowRegisters;
        _showPpu = settings.ShowPpu;
        _showDisasm = settings.ShowDisasm;
        _showStack = settings.ShowStack;
        _showZeroPage = settings.ShowZeroPage;
        _showMemoryEditor = settings.ShowMemoryEditor;
        _showMemoryPage = settings.ShowMemoryPage;
        _showModifiedMemory = settings.ShowModifiedMemory;
    }

    private void LoadPendingDisplaySettingsFromSystemConfig()
    {
        try
        {
            _isApplyingDisplaySettings = true;
            var settings = DebugDisplaySettingsController.LoadFromSystemConfig();
            _pendingShowRegisters = settings.ShowRegisters;
            _pendingShowPpu = settings.ShowPpu;
            _pendingShowDisasm = settings.ShowDisasm;
            _pendingShowStack = settings.ShowStack;
            _pendingShowZeroPage = settings.ShowZeroPage;
            _pendingShowMemoryEditor = settings.ShowMemoryEditor;
            _pendingShowMemoryPage = settings.ShowMemoryPage;
            _pendingShowModifiedMemory = settings.ShowModifiedMemory;
        }
        finally
        {
            _isApplyingDisplaySettings = false;
        }

        RaiseDisplaySettingsStatusProperties();
    }

    private void SavePendingDisplaySettingsToSystemConfig()
    {
        if (_isApplyingDisplaySettings)
            return;

        DebugDisplaySettingsController.SaveToSystemConfig(CreatePendingDisplaySettingsProfile());
    }

    private void RaiseDisplaySettingsStatusProperties()
    {
        OnPropertyChanged(nameof(HasPendingDisplaySettingsChanges));
        OnPropertyChanged(nameof(DisplaySettingsRestartHint));
    }

    private DebugWindowDisplaySettingsProfile CreateActiveDisplaySettingsProfile() =>
        CreateDisplaySettingsProfile(
            _showRegisters,
            _showPpu,
            _showDisasm,
            _showStack,
            _showZeroPage,
            _showMemoryEditor,
            _showMemoryPage,
            _showModifiedMemory);

    private DebugWindowDisplaySettingsProfile CreatePendingDisplaySettingsProfile() =>
        CreateDisplaySettingsProfile(
            _pendingShowRegisters,
            _pendingShowPpu,
            _pendingShowDisasm,
            _pendingShowStack,
            _pendingShowZeroPage,
            _pendingShowMemoryEditor,
            _pendingShowMemoryPage,
            _pendingShowModifiedMemory);

    private static DebugWindowDisplaySettingsProfile CreateDisplaySettingsProfile(
        bool showRegisters,
        bool showPpu,
        bool showDisasm,
        bool showStack,
        bool showZeroPage,
        bool showMemoryEditor,
        bool showMemoryPage,
        bool showModifiedMemory) => new()
        {
            ShowRegisters = showRegisters,
            ShowPpu = showPpu,
            ShowDisasm = showDisasm,
            ShowStack = showStack,
            ShowZeroPage = showZeroPage,
            ShowMemoryEditor = showMemoryEditor,
            ShowMemoryPage = showMemoryPage,
            ShowModifiedMemory = showModifiedMemory
        };

    private bool TryReadMemory(ushort address, out byte value)
    {
        try
        {
            value = _readMemory(address);
            return true;
        }
        catch (Exception ex)
        {
            value = 0;
            HandleDebugFailure($"读取内存 ${address:X4} 失败，当前游戏状态可能已损坏，请重新尝试。", ex);
            return false;
        }
    }

    private bool HasActiveRefreshSections()
    {
        return DebugLiveRefreshOrchestrator.HasActiveRefreshSections(
            ShowRegisters,
            ShowPpu,
            ShowDisasm,
            ShowStack,
            ShowZeroPage,
            ShowMemoryPage);
    }

    private void ScheduleRefresh()
    {
        if (!DebugLiveRefreshOrchestrator.ShouldScheduleRefresh(_refreshScheduled, _isDisposed, _hasSessionFailure))
            return;

        _refreshScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshScheduled = false;
            Refresh();
        }, DispatcherPriority.Background);
    }

    private void UpdateMemoryLocatorHighlights()
    {
        var memoryPageStart = DebugLiveRefreshOrchestrator.ResolveMemoryPageStart(_memoryPageIndex, MemoryPageSize);
        var updatedInPlace = DebugMemoryGridBuilder.TryUpdateMemoryRowsInPlace(
            MemoryRows,
            memoryPageStart,
            FlattenMemoryRowValues(MemoryRows),
            MemoryColumns,
            _highlightedMemoryAddress);
        if (DebugLiveRefreshOrchestrator.ShouldScheduleRefreshAfterLocatorUpdate(updatedInPlace))
            ScheduleRefresh();
    }

    private static byte[] FlattenMemoryRowValues(IEnumerable<MemoryPageRow> rows)
    {
        return rows
            .SelectMany(row => row.Cells)
            .Select(cell => DebugMemoryInputController.TryParseByte(cell.Value, out var value) ? value : (byte)0)
            .ToArray();
    }

    private void HandleDebugFailure(string message, Exception ex)
    {
        if (_hasSessionFailure)
            return;

        _hasSessionFailure = true;
        _liveRefreshTimer.Stop();
        _isMemoryCellEditing = false;
        EditStatus = message;
        RuntimeDiagnostics.Write("debug", $"{message} | {ex}");

        try
        {
            _notifySessionFailure?.Invoke(message);
        }
        catch (Exception notifyEx)
        {
            RuntimeDiagnostics.Write("debug", $"通知游戏会话故障失败: {notifyEx}");
        }
    }

    private DebugRefreshRequest CreateRefreshRequest() => new()
    {
        MemoryPageIndex = _memoryPageIndex,
        StackPageIndex = _stackPageIndex,
        ZeroPageSliceIndex = _zeroPageSliceIndex,
        DisasmPageIndex = _disasmPageIndex,
        CaptureRegisters = ShowRegisters,
        CapturePpu = ShowPpu,
        CaptureMemoryPage = ShowMemoryPage,
        CaptureStack = ShowStack,
        CaptureZeroPage = ShowZeroPage,
        CaptureDisasm = ShowDisasm
    };

    private void ReplaceRuntimeModifiedMemoryEntries()
    {
        if (_replaceModifiedMemoryRuntimeEntries == null)
            return;

        var runtimeEntries = DebugModifiedMemoryRuntimeSyncController.BuildRuntimeEntries(ModifiedMemoryEntries);
        _replaceModifiedMemoryRuntimeEntries(runtimeEntries);
    }

}
