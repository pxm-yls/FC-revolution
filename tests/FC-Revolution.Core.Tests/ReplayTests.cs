using FCRevolution.Core;
using FCRevolution.Core.Input;
using FCRevolution.Core.Replay;
using FCRevolution.Core.Timeline.Persistence;
using StorageFrameInputRecord = FCRevolution.Storage.FrameInputRecord;
using StorageReplayLogReader = FCRevolution.Storage.ReplayLogReader;
using StorageReplayLogWriter = FCRevolution.Storage.ReplayLogWriter;

namespace FC_Revolution.Core.Tests;

public class ReplayLogTests
{
    [Fact]
    public void ReplayLogWriter_AndReader_RoundTripRecords()
    {
        var path = Path.Combine(Path.GetTempPath(), $"replay-{Guid.NewGuid():N}.bin");

        try
        {
            using (var writer = new StorageReplayLogWriter())
            {
                writer.Open(path, resetFile: true);
                writer.Append(ReplayTestData.CreateRecord(1, ("p1", "a")));
                writer.Append(ReplayTestData.CreateRecord(2, ("p1", "a"), ("p1", "b")));
                writer.Flush();
            }

            var records = StorageReplayLogReader.ReadAll(path);
            Assert.Collection(records,
                first =>
                {
                    Assert.Equal(1, first.Frame);
                    Assert.True(first.IsActionPressed("p1", "a"));
                },
                second =>
                {
                    Assert.Equal(2, second.Frame);
                    Assert.True(second.IsActionPressed("p1", "a"));
                    Assert.True(second.IsActionPressed("p1", "b"));
                });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ReplayLogReader_ReadRange_ReturnsExclusiveInclusiveWindow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"replay-range-{Guid.NewGuid():N}.bin");

        try
        {
            using (var writer = new StorageReplayLogWriter())
            {
                writer.Open(path, resetFile: true);
                writer.Append(ReplayTestData.CreateRecord(1, ("p1", "a")));
                writer.Append(ReplayTestData.CreateRecord(2, ("p1", "b")));
                writer.Append(ReplayTestData.CreateRecord(3, ("p1", "a"), ("p1", "b")));
                writer.Append(ReplayTestData.CreateRecord(4, ("p1", "select")));
                writer.Flush();
            }

            var records = StorageReplayLogReader.ReadRange(path, startExclusiveFrame: 1, endInclusiveFrame: 3).ToList();

            Assert.Collection(records,
                first =>
                {
                    Assert.Equal(2, first.Frame);
                    Assert.True(first.IsActionPressed("p1", "b"));
                },
                second =>
                {
                    Assert.Equal(3, second.Frame);
                    Assert.True(second.IsActionPressed("p1", "a"));
                    Assert.True(second.IsActionPressed("p1", "b"));
                });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ReplayLogReader_ReadAll_TranslatesLegacyMaskRecordsToActions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy-replay-{Guid.NewGuid():N}.bin");

        try
        {
            using (var stream = File.Create(path))
            {
                stream.Write("FCRL"u8);
                stream.WriteByte(2);
                stream.WriteByte(2);
                ReplayTestData.WriteSizedString(stream, "p1");
                ReplayTestData.WriteSizedString(stream, "p2");
                ReplayTestData.WriteLegacyRecord(stream, 1, 0x01, 0x00);
                ReplayTestData.WriteLegacyRecord(stream, 2, 0x82, 0x00);
                stream.Flush();
            }

            var records = StorageReplayLogReader.ReadAll(path);

            Assert.Collection(
                records,
                first =>
                {
                    Assert.True(first.IsActionPressed("p1", "a"));
                    Assert.False(first.IsActionPressed("p1", "b"));
                },
                second =>
                {
                    Assert.True(second.IsActionPressed("p1", "b"));
                    Assert.True(second.IsActionPressed("p1", "right"));
                });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

public class ReplayPlayerTests
{
    [Fact]
    public void ReplayPlayer_SeekToFrame_ReplaysForwardWithoutThrowing()
    {
        var romPath = CreateTestRomFile();

        try
        {
            var nes = new NesConsole();
            nes.LoadRom(romPath);
            var baseState = nes.SaveState();

            var records = new[]
            {
                ReplayTestData.CreateRecord(1, ("p1", "a")),
                ReplayTestData.CreateRecord(2),
                ReplayTestData.CreateRecord(3, ("p1", "right")),
            };

            var player = new ReplayPlayer(romPath, baseState, records);
            var frame = player.SeekToFrame(3);

            Assert.NotNull(frame);
            Assert.Equal(256 * 240, frame.Length);
            Assert.True(player.Console.Ppu.Frame >= 3);
        }
        finally
        {
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ReplayPlayer_RenderFrameRange_ReturnsRequestedFrames()
    {
        var romPath = CreateTestRomFile();

        try
        {
            var nes = new NesConsole();
            nes.LoadRom(romPath);
            var baseState = nes.SaveState();

            var records = Enumerable.Range(1, 5)
                .Select(frame => frame % 2 == 0
                    ? ReplayTestData.CreateRecord(frame, ("p1", "a"))
                    : ReplayTestData.CreateRecord(frame))
                .ToArray();

            var player = new ReplayPlayer(romPath, baseState, records);
            var frames = player.RenderFrameRange(2, 4);

            Assert.Equal(3, frames.Count);
            Assert.All(frames, frame => Assert.Equal(256 * 240, frame.Length));
        }
        finally
        {
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void ReplayPlayer_RendersFromNonZeroBaseFrame()
    {
        var romPath = CreateTestRomFile();

        try
        {
            var nes = new NesConsole();
            nes.LoadRom(romPath);
            nes.RunFrame();
            nes.RunFrame();
            var baseState = nes.SaveState();
            var baseFrame = nes.Ppu.Frame;

            var records = new[]
            {
                ReplayTestData.CreateRecord(baseFrame),
                ReplayTestData.CreateRecord(baseFrame + 1, ("p1", "a")),
                ReplayTestData.CreateRecord(baseFrame + 2, ("p1", "right")),
            };

            var player = new ReplayPlayer(romPath, baseState, records);
            var frames = player.RenderFrameRange(baseFrame + 1, baseFrame + 2);

            Assert.Equal(2, frames.Count);
            Assert.True(player.Console.Ppu.Frame >= baseFrame + 2);
        }
        finally
        {
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }

    [Fact]
    public void SetControllerMask_AppliesPressedButtons()
    {
        var controller = new StandardController();
        var bus = new FCRevolution.Core.Bus.NesBus
        {
            Controller1 = controller
        };
        var nes = new NesConsole();
        nes.Bus.Controller1 = controller;

        nes.SetControllerMask(0, (byte)(NesButton.A | NesButton.Right));
        controller.Write(1);
        controller.Write(0);

        Assert.Equal(1, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(0, controller.ReadState() & 0x01);
        Assert.Equal(1, controller.ReadState() & 0x01);
    }

    private static string CreateTestRomFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-rom-{Guid.NewGuid():N}.nes");
        File.WriteAllBytes(path, CreateMinimalTestRom());
        return path;
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
        rom[prgStart + 0x0000] = 0xEA; // NOP
        rom[prgStart + 0x0001] = 0x4C; // JMP $8000
        rom[prgStart + 0x0002] = 0x00;
        rom[prgStart + 0x0003] = 0x80;

        // Reset/NMI/IRQ vectors at $FFFA-$FFFF in 16K mirrored bank
        rom[prgStart + 0x3FFA] = 0x00;
        rom[prgStart + 0x3FFB] = 0x80;
        rom[prgStart + 0x3FFC] = 0x00;
        rom[prgStart + 0x3FFD] = 0x80;
        rom[prgStart + 0x3FFE] = 0x00;
        rom[prgStart + 0x3FFF] = 0x80;

        return rom;
    }

}

internal static class ReplayTestData
{
    public static void WriteSizedString(Stream stream, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes);
    }

    public static void WriteLegacyRecord(Stream stream, long frame, byte p1Mask, byte p2Mask)
    {
        Span<byte> frameBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(frameBytes, frame);
        stream.Write(frameBytes);
        stream.WriteByte(p1Mask);
        stream.WriteByte(p2Mask);
    }

    public static StorageFrameInputRecord CreateRecord(long frame, params (string PortId, string ActionId)[] actions)
    {
        var actionsByPort = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (portId, actionId) in actions)
        {
            if (!actionsByPort.TryGetValue(portId, out var actionSet))
            {
                actionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                actionsByPort[portId] = actionSet;
            }

            actionSet.Add(actionId);
        }

        return new StorageFrameInputRecord(
            frame,
            actionsByPort.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlySet<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase));
    }
}

public class BranchScopedQuickSaveTests
{
    [Fact]
    public void BranchScopedQuickSave_UsesIndependentPathsAndRecords()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Scoped Save Rom");
            var mainBranchId = manifest.CurrentBranchId;
            var otherBranchId = Guid.NewGuid();
            repository.EnsureBranch(manifest, otherBranchId, "分支 2");

            var mainQuickSave = repository.UpsertQuickSaveSnapshot(manifest, mainBranchId, 120, 2.0);
            var otherQuickSave = repository.UpsertQuickSaveSnapshot(manifest, otherBranchId, 240, 4.0);
            repository.Save(manifest);

            Assert.NotEqual(mainQuickSave.StateFile, otherQuickSave.StateFile);
            Assert.Equal(TimelineStoragePaths.GetQuickSavePath(romId, mainBranchId), mainQuickSave.StateFile);
            Assert.Equal(TimelineStoragePaths.GetQuickSavePath(romId, otherBranchId), otherQuickSave.StateFile);

            var mainBranch = Assert.Single(manifest.Branches, branch => branch.BranchId == mainBranchId);
            var otherBranch = Assert.Single(manifest.Branches, branch => branch.BranchId == otherBranchId);
            Assert.Equal(mainQuickSave.SnapshotId, mainBranch.QuickSaveSnapshotId);
            Assert.Equal(otherQuickSave.SnapshotId, otherBranch.QuickSaveSnapshotId);
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }
}
