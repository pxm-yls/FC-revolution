using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowBranchExportExecutionControllerTests
{
    [Fact]
    public void Execute_WithBranchSnapshotPath_UsesSnapshotPathAndRegisters()
    {
        var exportedPaths = new List<string>();
        var registered = new List<string>();

        var plan = new MainWindowBranchExportPlan(
            BranchId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RomPath: "/tmp/demo.nes",
            InputLogPath: "/tmp/input.bin",
            OutputPath: "/tmp/export.mp4",
            SnapshotPath: "/tmp/branch.fcsnap",
            SnapshotBytes: null);

        var runtimeHost = new MainWindowBranchExportRuntimeHost(
            exportMp4: (rom, snapshot, log, start, end, output) =>
            {
                exportedPaths.Add($"{rom}|{snapshot}|{log}|{start}|{end}|{output}");
                return "/tmp/exported.mp4";
            },
            registerExport: (rom, branchId, start, end, exportedPath) =>
                registered.Add($"{rom}|{branchId:N}|{start}-{end}|{exportedPath}"),
            createTempSnapshotPath: () => throw new InvalidOperationException("should not run"),
            writeBytes: (_, _) => throw new InvalidOperationException("should not run"),
            deleteFile: _ => throw new InvalidOperationException("should not run"));

        var controller = new MainWindowBranchExportExecutionController(
            runtimeHost,
            runInBackground: work => Task.FromResult(work()));

        var result = controller.Execute(plan, startFrame: 120, endFrame: 180);

        Assert.Equal("/tmp/exported.mp4", result);
        Assert.Equal(
            ["/tmp/demo.nes|/tmp/branch.fcsnap|/tmp/input.bin|120|180|/tmp/export.mp4"],
            exportedPaths);
        Assert.Equal(
            ["/tmp/demo.nes|11111111111111111111111111111111|120-180|/tmp/exported.mp4"],
            registered);
    }

    [Fact]
    public void Execute_WithMainlineSnapshot_WritesTempFile_RegistersAndDeletes()
    {
        var tempPath = "/tmp/temp-export.fcsnap";
        var written = new List<string>();
        var deleted = new List<string>();
        var registered = new List<string>();

        var plan = new MainWindowBranchExportPlan(
            BranchId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RomPath: "/tmp/demo.nes",
            InputLogPath: "/tmp/input.bin",
            OutputPath: "/tmp/export.mp4",
            SnapshotPath: null,
            SnapshotBytes: [1, 2, 3]);

        var runtimeHost = new MainWindowBranchExportRuntimeHost(
            exportMp4: (rom, snapshot, log, start, end, output) =>
            {
                Assert.Equal("/tmp/demo.nes", rom);
                Assert.Equal(tempPath, snapshot);
                Assert.Equal("/tmp/input.bin", log);
                Assert.Equal(120, start);
                Assert.Equal(220, end);
                Assert.Equal("/tmp/export.mp4", output);
                return "/tmp/mainline-export.mp4";
            },
            registerExport: (_, branchId, start, end, exportedPath) =>
                registered.Add($"{branchId:N}|{start}-{end}|{exportedPath}"),
            createTempSnapshotPath: () => tempPath,
            writeBytes: (path, bytes) =>
            {
                written.Add(path);
                Assert.Equal(plan.SnapshotBytes, bytes);
            },
            deleteFile: path => deleted.Add(path));

        var controller = new MainWindowBranchExportExecutionController(
            runtimeHost,
            runInBackground: work => Task.FromResult(work()));

        var result = controller.Execute(plan, startFrame: 120, endFrame: 220);

        Assert.Equal("/tmp/mainline-export.mp4", result);
        Assert.Equal([tempPath], written);
        Assert.Equal([tempPath], deleted);
        Assert.Equal(
            ["22222222222222222222222222222222|120-220|/tmp/mainline-export.mp4"],
            registered);
    }

    [Fact]
    public void Execute_WhenExporterThrows_DeletesTempFileAndSkipsRegister()
    {
        var tempPath = "/tmp/temp-export.fcsnap";
        var deleted = new List<string>();
        var registered = new List<string>();

        var plan = new MainWindowBranchExportPlan(
            BranchId: Guid.NewGuid(),
            RomPath: "/tmp/demo.nes",
            InputLogPath: "/tmp/input.bin",
            OutputPath: "/tmp/export.mp4",
            SnapshotPath: null,
            SnapshotBytes: [1]);

        var runtimeHost = new MainWindowBranchExportRuntimeHost(
            exportMp4: (_, _, _, _, _, _) => throw new InvalidOperationException("fail"),
            registerExport: (_, _, _, _, _) => registered.Add("called"),
            createTempSnapshotPath: () => tempPath,
            writeBytes: (_, _) => { },
            deleteFile: deleted.Add);

        var controller = new MainWindowBranchExportExecutionController(
            runtimeHost,
            runInBackground: work => Task.FromResult(work()));

        var ex = Assert.Throws<InvalidOperationException>(() => controller.Execute(plan, 10, 20));

        Assert.Equal("fail", ex.Message);
        Assert.Equal([tempPath], deleted);
        Assert.Empty(registered);
    }

    [Fact]
    public void Execute_WhenWritingTempSnapshotFails_DeletesTempFileAndSkipsExportAndRegister()
    {
        var tempPath = "/tmp/temp-export.fcsnap";
        var deleted = new List<string>();
        var exported = new List<string>();
        var registered = new List<string>();

        var plan = new MainWindowBranchExportPlan(
            BranchId: Guid.NewGuid(),
            RomPath: "/tmp/demo.nes",
            InputLogPath: "/tmp/input.bin",
            OutputPath: "/tmp/export.mp4",
            SnapshotPath: null,
            SnapshotBytes: [9, 9, 9]);

        var runtimeHost = new MainWindowBranchExportRuntimeHost(
            exportMp4: (_, _, _, _, _, _) =>
            {
                exported.Add("called");
                return "/tmp/exported.mp4";
            },
            registerExport: (_, _, _, _, _) => registered.Add("called"),
            createTempSnapshotPath: () => tempPath,
            writeBytes: (_, _) => throw new IOException("disk full"),
            deleteFile: deleted.Add);

        var controller = new MainWindowBranchExportExecutionController(
            runtimeHost,
            runInBackground: work => Task.FromResult(work()));

        var ex = Assert.Throws<IOException>(() => controller.Execute(plan, 10, 20));

        Assert.Equal("disk full", ex.Message);
        Assert.Equal([tempPath], deleted);
        Assert.Empty(exported);
        Assert.Empty(registered);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBackgroundRunner()
    {
        var scheduled = 0;
        var plan = new MainWindowBranchExportPlan(
            BranchId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            RomPath: "/tmp/demo.nes",
            InputLogPath: "/tmp/input.bin",
            OutputPath: "/tmp/export.mp4",
            SnapshotPath: "/tmp/branch.fcsnap",
            SnapshotBytes: null);

        var runtimeHost = new MainWindowBranchExportRuntimeHost(
            exportMp4: (_, _, _, _, _, _) => "/tmp/exported.mp4",
            registerExport: (_, _, _, _, _) => { },
            createTempSnapshotPath: () => throw new InvalidOperationException("should not run"),
            writeBytes: (_, _) => throw new InvalidOperationException("should not run"),
            deleteFile: _ => throw new InvalidOperationException("should not run"));

        var controller = new MainWindowBranchExportExecutionController(
            runtimeHost,
            runInBackground: work =>
            {
                scheduled++;
                return Task.FromResult(work());
            });

        var result = await controller.ExecuteAsync(plan, 33, 66);

        Assert.Equal("/tmp/exported.mp4", result);
        Assert.Equal(1, scheduled);
    }

    [Fact]
    public void BuildExportObjectName_UsesBranchAndFrameRange()
    {
        var name = MainWindowBranchExportExecutionController.BuildExportObjectName(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            startFrame: 12,
            endFrame: 48);

        Assert.Equal("exports.branch.33333333333333333333333333333333.12-48.mp4", name);
    }
}
