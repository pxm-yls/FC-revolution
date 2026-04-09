using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public class DebugViewModelTests : IDisposable
{
    private const string ResourceRootOverrideEnvironmentVariableName = "FC_REVOLUTION_RESOURCE_ROOT";
    private readonly string? _originalResourceRootOverride;
    private readonly string _originalResourceRoot;
    private readonly string _isolatedDefaultResourceRoot;

    public DebugViewModelTests()
    {
        _originalResourceRootOverride = Environment.GetEnvironmentVariable(ResourceRootOverrideEnvironmentVariableName);
        _originalResourceRoot = AppObjectStorage.GetResourceRoot();
        _isolatedDefaultResourceRoot = CreateTempResourceRoot();
        Environment.SetEnvironmentVariable(ResourceRootOverrideEnvironmentVariableName, _isolatedDefaultResourceRoot);
        AppObjectStorage.ConfigureResourceRoot(null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ResourceRootOverrideEnvironmentVariableName, _originalResourceRootOverride);
        AppObjectStorage.ConfigureResourceRoot(_originalResourceRoot);
        SafeDeleteDirectory(_isolatedDefaultResourceRoot);
    }

    [Fact]
    public void Refresh_UsesBulkSnapshot_WhenAvailable()
    {
        var romPath = CreateTestRomFile();
        var directReadCount = 0;
        var snapshotCount = 0;
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: _ =>
            {
                directReadCount++;
                return 0xFF;
            },
            writeMemory: (_, _) => { },
            captureRefreshSnapshot: _ =>
            {
                snapshotCount++;
                return new DebugRefreshSnapshot
                {
                    State = CreateDebugState(0x8000, scanline: 10, dot: 20, frame: 30),
                    MemoryPageStart = 0x0000,
                    MemoryPage = BuildRange(DebugViewModel.MemoryPageSize, 0x10),
                    StackPageStart = 0x0100,
                    StackPage = BuildRange(DebugViewModel.StackPageSize, 0x20),
                    ZeroPageStart = 0x0000,
                    ZeroPage = BuildRange(DebugViewModel.ZeroPageSliceSize, 0x30),
                    DisasmStart = 0x8000,
                    Disasm = BuildRange(DebugViewModel.DisasmPageSize, 0x40)
                };
            },
            activeDisplaySettings: new DebugWindowDisplaySettingsProfile
            {
                ShowRegisters = true,
                ShowPpu = true,
                ShowDisasm = true,
                ShowStack = true,
                ShowZeroPage = true,
                ShowMemoryEditor = true,
                ShowMemoryPage = true,
                ShowModifiedMemory = true
            },
            enableLiveRefresh: false);

        try
        {
            vm.Refresh();

            Assert.Equal(1, snapshotCount);
            Assert.Equal(0, directReadCount);
            Assert.Equal("10", vm.MemoryRows[0].Cells[0].Value);
            Assert.Equal("20", vm.StackRows[0].Cells[0].Value);
            Assert.Equal("30", vm.ZeroPageRows[0].Cells[0].Value);
            Assert.StartsWith(">40", vm.DisasmRows[0].Cells[0].Value);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ReadMemory_HighlightsQueriedAddressInMemoryPage()
    {
        var romPath = CreateTestRomFile();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            enableLiveRefresh: false);

        try
        {
            vm.AddressInput = "0085";

            vm.ReadMemoryCommand.Execute(null);

            var highlightedCell = vm.MemoryRows
                .SelectMany(row => row.Cells)
                .Single(cell => cell.Address == 0x0085);

            Assert.True(highlightedCell.IsHighlighted);
            Assert.Equal("85", highlightedCell.Value);
            Assert.Equal("2", vm.MemoryPageInput);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ReadMemory_AddsRowAndColumnLocatorHighlights()
    {
        var romPath = CreateTestRomFile();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            enableLiveRefresh: false);

        try
        {
            vm.AddressInput = "0012";

            vm.ReadMemoryCommand.Execute(null);

            var selectedCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0012);
            var sameRowCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0015);
            var sameColumnCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0022);
            var unrelatedCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0034);

            Assert.True(selectedCell.IsHighlighted);
            Assert.True(selectedCell.IsRowLocatorHighlighted);
            Assert.True(selectedCell.IsColumnLocatorHighlighted);
            Assert.True(sameRowCell.IsRowLocatorHighlighted);
            Assert.False(sameRowCell.IsColumnLocatorHighlighted);
            Assert.True(sameColumnCell.IsColumnLocatorHighlighted);
            Assert.False(sameColumnCell.IsRowLocatorHighlighted);
            Assert.False(unrelatedCell.IsRowLocatorHighlighted);
            Assert.False(unrelatedCell.IsColumnLocatorHighlighted);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void SelectMemoryCell_UpdatesLocatorHighlightsInPlace()
    {
        var romPath = CreateTestRomFile();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            enableLiveRefresh: false);

        try
        {
            vm.AddressInput = "0011";
            vm.ReadMemoryCommand.Execute(null);

            var rowReferencesBefore = vm.MemoryRows.ToArray();
            var selectedCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0022);
            vm.SelectMemoryCellCommand.Execute(selectedCell);

            Assert.Equal("0022", vm.AddressInput);
            Assert.Equal("22", vm.ValueInput);
            Assert.Equal(rowReferencesBefore.Length, vm.MemoryRows.Count);
            for (var index = 0; index < rowReferencesBefore.Length; index++)
                Assert.Same(rowReferencesBefore[index], vm.MemoryRows[index]);

            var oldCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0011);
            var newCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0022);
            var sameRowCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0025);
            var sameColumnCell = vm.MemoryRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x0032);

            Assert.False(oldCell.IsHighlighted);
            Assert.True(newCell.IsHighlighted);
            Assert.True(newCell.IsRowLocatorHighlighted);
            Assert.True(newCell.IsColumnLocatorHighlighted);
            Assert.True(sameRowCell.IsRowLocatorHighlighted);
            Assert.False(sameRowCell.IsColumnLocatorHighlighted);
            Assert.True(sameColumnCell.IsColumnLocatorHighlighted);
            Assert.False(sameColumnCell.IsRowLocatorHighlighted);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ModifiedMemoryEntries_EnablePagination_AfterEleventhItem()
    {
        var romPath = CreateTestRomFile();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            enableLiveRefresh: false);

        try
        {
            for (var index = 0; index < 11; index++)
            {
                vm.AddressInput = index.ToString("X4");
                vm.ValueInput = (index + 1).ToString("X2");
                vm.WriteMemoryCommand.Execute(null);
            }

            Assert.True(vm.ShowModifiedMemoryPagination);
            Assert.Equal(10, vm.VisibleModifiedMemoryEntries.Count);
            Assert.Equal("第 1 / 2 页", vm.ModifiedMemoryPageSummary);

            vm.NextModifiedMemoryPageCommand.Execute(null);

            Assert.Single(vm.VisibleModifiedMemoryEntries);
            Assert.Equal("$0000", vm.VisibleModifiedMemoryEntries.Single().DisplayAddress);
            Assert.Equal("第 2 / 2 页", vm.ModifiedMemoryPageSummary);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void WriteMemory_UpdatesPageStatusAndRuntimeCallback()
    {
        var romPath = CreateTestRomFile();
        var runtimeUpdates = new List<ModifiedMemoryRuntimeEntry>();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            upsertModifiedMemoryRuntimeEntry: entry => runtimeUpdates.Add(entry),
            activeDisplaySettings: new DebugWindowDisplaySettingsProfile
            {
                ShowMemoryEditor = true,
                ShowMemoryPage = true,
                ShowModifiedMemory = true
            },
            enableLiveRefresh: false);

        try
        {
            vm.AddressInput = "0188";
            vm.ValueInput = "7F";

            vm.WriteMemoryCommand.Execute(null);

            Assert.Equal("4", vm.MemoryPageInput);
            Assert.Equal("已修改 $0188 = $7F", vm.EditStatus);
            Assert.Single(runtimeUpdates);
            Assert.Equal(new ModifiedMemoryRuntimeEntry(0x0188, 0x7F, false), runtimeUpdates[0]);

            var highlightedCell = vm.MemoryRows
                .SelectMany(row => row.Cells)
                .Single(cell => cell.Address == 0x0188);
            Assert.True(highlightedCell.IsHighlighted);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void CommitMemoryCellEdit_WhenWriteThrows_NotifiesSessionFailureWithoutThrowing()
    {
        var romPath = CreateTestRomFile();
        string? failureMessage = null;
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: _ => 0x10,
            writeMemory: (_, _) => throw new InvalidOperationException("boom"),
            notifySessionFailure: message => failureMessage = message,
            enableLiveRefresh: false);

        try
        {
            var cell = new MemoryCellItem
            {
                Address = 0x0010,
                DisplayAddress = "$0010",
                Value = "FF"
            };

            var ex = Record.Exception(() => vm.CommitMemoryCellEdit(cell));

            Assert.Null(ex);
            Assert.NotNull(failureMessage);
            Assert.Contains("你修改的内存值 $0010 = $FF", failureMessage);
            Assert.Equal(failureMessage, vm.EditStatus);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void Constructor_LoadsDisplaySettingsFromSystemConfig()
    {
        var romPath = CreateTestRomFile();
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                DebugWindowDisplaySettings = new DebugWindowDisplaySettingsProfile
                {
                    ShowRegisters = false,
                    ShowPpu = false,
                    ShowDisasm = true,
                    ShowStack = true,
                    ShowZeroPage = false,
                    ShowMemoryEditor = true,
                    ShowMemoryPage = false,
                    ShowModifiedMemory = true
                }
            });

            var vm = new DebugViewModel(
                "Test Game",
                romPath,
                new FakeDebugSurface(),
                captureDebugState: CreateDebugState,
                readMemory: address => (byte)(address & 0xFF),
                writeMemory: (_, _) => { },
                enableLiveRefresh: false);

            try
            {
                Assert.False(vm.ShowRegisters);
                Assert.False(vm.ShowPpu);
                Assert.True(vm.ShowDisasm);
                Assert.True(vm.ShowStack);
                Assert.False(vm.ShowZeroPage);
                Assert.True(vm.ShowMemoryEditor);
                Assert.False(vm.ShowMemoryPage);
                Assert.True(vm.ShowModifiedMemory);
            }
            finally
            {
                vm.Dispose();
            }
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ChangingSectionVisibility_PersistsToSystemConfig()
    {
        var romPath = CreateTestRomFile();
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);

            var vm = new DebugViewModel(
                "Test Game",
                romPath,
                new FakeDebugSurface(),
                captureDebugState: CreateDebugState,
                readMemory: address => (byte)(address & 0xFF),
                writeMemory: (_, _) => { },
                enableLiveRefresh: false);

            try
            {
                vm.PendingShowRegisters = true;
                vm.PendingShowPpu = true;
                vm.PendingShowModifiedMemory = false;

                var profile = SystemConfigProfile.Load();

                Assert.True(profile.DebugWindowDisplaySettings.ShowRegisters);
                Assert.True(profile.DebugWindowDisplaySettings.ShowPpu);
                Assert.False(profile.DebugWindowDisplaySettings.ShowDisasm);
                Assert.False(profile.DebugWindowDisplaySettings.ShowStack);
                Assert.False(profile.DebugWindowDisplaySettings.ShowZeroPage);
                Assert.True(profile.DebugWindowDisplaySettings.ShowMemoryEditor);
                Assert.True(profile.DebugWindowDisplaySettings.ShowMemoryPage);
                Assert.False(profile.DebugWindowDisplaySettings.ShowModifiedMemory);
            }
            finally
            {
                vm.Dispose();
            }
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void MissingSystemConfig_UsesLeftHiddenRightVisibleDefaults()
    {
        var romPath = CreateTestRomFile();
        var resourceRoot = CreateTempResourceRoot();
        var previousRoot = AppObjectStorage.GetResourceRoot();

        try
        {
            AppObjectStorage.ConfigureResourceRoot(resourceRoot);

            var vm = new DebugViewModel(
                "Test Game",
                romPath,
                new FakeDebugSurface(),
                captureDebugState: CreateDebugState,
                readMemory: address => (byte)(address & 0xFF),
                writeMemory: (_, _) => { },
                enableLiveRefresh: false);

            try
            {
                Assert.False(vm.ShowRegisters);
                Assert.False(vm.ShowPpu);
                Assert.False(vm.ShowDisasm);
                Assert.False(vm.ShowStack);
                Assert.False(vm.ShowZeroPage);
                Assert.True(vm.ShowMemoryEditor);
                Assert.True(vm.ShowMemoryPage);
                Assert.True(vm.ShowModifiedMemory);
            }
            finally
            {
                vm.Dispose();
            }
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(previousRoot);
            SafeDeleteDirectory(resourceRoot);
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void PendingDisplaySettings_DoNotChangeActiveSessionLayout()
    {
        var romPath = CreateTestRomFile();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            activeDisplaySettings: new DebugWindowDisplaySettingsProfile
            {
                ShowRegisters = true,
                ShowPpu = false,
                ShowDisasm = false,
                ShowStack = false,
                ShowZeroPage = false,
                ShowMemoryEditor = true,
                ShowMemoryPage = true,
                ShowModifiedMemory = true
            },
            enableLiveRefresh: false);

        try
        {
            Assert.True(vm.ShowLeftPane);
            Assert.True(vm.ShowRightPane);
            Assert.Equal(1200, vm.PreferredWindowWidth);

            vm.PendingShowRegisters = false;

            Assert.True(vm.ShowRegisters);
            Assert.False(vm.PendingShowRegisters);
            Assert.True(vm.ShowLeftPane);
            Assert.True(vm.ShowRightPane);
            Assert.Equal(1200, vm.PreferredWindowWidth);
            Assert.True(vm.HasPendingDisplaySettingsChanges);
            Assert.Equal("新设置已保存，重启当前游戏后才会同步影响显示与统计。", vm.DisplaySettingsRestartHint);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void RefreshRequest_SkipsHiddenSections()
    {
        var romPath = CreateTestRomFile();
        DebugRefreshRequest? capturedRequest = null;
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            captureRefreshSnapshot: request =>
            {
                capturedRequest = request;
                return new DebugRefreshSnapshot
                {
                    State = CreateDebugState(),
                    MemoryPageStart = 0x0000,
                    MemoryPage = BuildRange(DebugViewModel.MemoryPageSize, 0x10)
                };
            },
            activeDisplaySettings: new DebugWindowDisplaySettingsProfile
            {
                ShowRegisters = false,
                ShowPpu = false,
                ShowDisasm = false,
                ShowStack = false,
                ShowZeroPage = false,
                ShowMemoryEditor = true,
                ShowMemoryPage = true,
                ShowModifiedMemory = true
            },
            enableLiveRefresh: false);

        try
        {
            vm.Refresh();

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.CaptureRegisters);
            Assert.False(capturedRequest.CapturePpu);
            Assert.False(capturedRequest.CaptureDisasm);
            Assert.False(capturedRequest.CaptureStack);
            Assert.False(capturedRequest.CaptureZeroPage);
            Assert.True(capturedRequest.CaptureMemoryPage);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ToggleSavedMemoryLock_UpdatesRuntimeLockCallback()
    {
        var romPath = CreateTestRomFile();
        var updates = new List<ModifiedMemoryRuntimeEntry>();
        var vm = new DebugViewModel(
            "Test Game",
            romPath,
            new FakeDebugSurface(),
            captureDebugState: CreateDebugState,
            readMemory: address => (byte)(address & 0xFF),
            writeMemory: (_, _) => { },
            upsertModifiedMemoryRuntimeEntry: entry => updates.Add(entry),
            enableLiveRefresh: false);

        try
        {
            vm.AddressInput = "0001";
            vm.ValueInput = "7F";
            vm.WriteMemoryCommand.Execute(null);

            updates.Clear();
            var entry = vm.ModifiedMemoryEntries.Single();
            vm.ToggleSavedMemoryLockCommand.Execute(entry);

            Assert.True(entry.IsLocked);
            Assert.Single(updates);
            Assert.Equal((ushort)0x0001, updates[0].Address);
            Assert.Equal((byte)0x7F, updates[0].Value);
            Assert.True(updates[0].IsLocked);
        }
        finally
        {
            vm.Dispose();
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    private static CoreDebugState CreateDebugState() =>
        CreateDebugState(0x8000);

    private static CoreDebugState CreateDebugState(
        ushort instructionPointer,
        int scanline = 0,
        int dot = 0,
        long frame = 0) =>
        new()
        {
            InstructionPointer = instructionPointer,
            InstructionPointerLabel = "PC",
            Sections =
            [
                new CoreDebugSection(
                    "cpu-registers",
                    "CPU Registers",
                    "registers",
                    [
                        new CoreDebugValue("A", "01"),
                        new CoreDebugValue("X", "02"),
                        new CoreDebugValue("Y", "03"),
                        new CoreDebugValue("S", "FD")
                    ]),
                new CoreDebugSection(
                    "cpu-status",
                    "CPU Status",
                    "registers",
                    [
                        new CoreDebugValue("PC", $"{instructionPointer:X4}"),
                        new CoreDebugValue("P", "24"),
                        new CoreDebugValue("Flags", "NV-BDIZC"),
                        new CoreDebugValue("Cycles", "0")
                    ]),
                new CoreDebugSection(
                    "video-timing",
                    "Video Timing",
                    "video",
                    [
                        new CoreDebugValue("Scanline", scanline.ToString()),
                        new CoreDebugValue("Dot", dot.ToString()),
                        new CoreDebugValue("Frame", frame.ToString())
                    ])
            ]
        };

    private sealed class FakeDebugSurface : ICoreDebugSurface
    {
        private readonly Dictionary<ushort, byte> _memory = [];

        public CoreDebugState CaptureDebugState() => CreateDebugState();

        public byte ReadMemory(ushort address) =>
            _memory.TryGetValue(address, out var value) ? value : (byte)0;

        public void WriteMemory(ushort address, byte value) => _memory[address] = value;

        public byte[] ReadMemoryBlock(ushort startAddress, int length)
        {
            var values = new byte[length];
            for (var index = 0; index < length; index++)
                values[index] = ReadMemory(unchecked((ushort)(startAddress + index)));

            return values;
        }
    }

    private static byte[] BuildRange(int length, byte start)
    {
        var values = new byte[length];
        for (var index = 0; index < values.Length; index++)
            values[index] = unchecked((byte)(start + index));

        return values;
    }

    private static string CreateTestRomFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"debug-view-model-test-rom-{Guid.NewGuid():N}.nes");
        File.WriteAllBytes(path, CreateMinimalTestRom());
        return path;
    }

    private static string CreateTempResourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fc-debug-view-model-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static byte[] CreateMinimalTestRom()
    {
        var rom = new byte[16 + 16384 + 8192];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;

        var prgStart = 16;
        rom[prgStart + 0x0000] = 0xEA;
        rom[prgStart + 0x0001] = 0x4C;
        rom[prgStart + 0x0002] = 0x00;
        rom[prgStart + 0x0003] = 0x80;

        rom[prgStart + 0x3FFA] = 0x00;
        rom[prgStart + 0x3FFB] = 0x80;
        rom[prgStart + 0x3FFC] = 0x00;
        rom[prgStart + 0x3FFD] = 0x80;
        rom[prgStart + 0x3FFE] = 0x00;
        rom[prgStart + 0x3FFF] = 0x80;

        return rom;
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
