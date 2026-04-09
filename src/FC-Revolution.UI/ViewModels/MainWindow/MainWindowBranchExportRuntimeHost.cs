using System;
using System.IO;
using FCRevolution.Storage;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowBranchExportRuntimeHost
{
    private readonly Func<string, string, string, long, long, string, string> _exportMp4;
    private readonly Action<string, Guid, long, long, string> _registerExport;
    private readonly Func<string> _createTempSnapshotPath;
    private readonly Action<string, byte[]> _writeBytes;
    private readonly Action<string> _deleteFile;

    public MainWindowBranchExportRuntimeHost()
        : this(
            TimelineVideoExporter.ExportMp4,
            RegisterExportObject,
            () => Path.Combine(Path.GetTempPath(), $"fcr-export-{Guid.NewGuid():N}.fcsnap"),
            File.WriteAllBytes,
            path =>
            {
                if (File.Exists(path))
                    File.Delete(path);
            })
    {
    }

    internal MainWindowBranchExportRuntimeHost(
        Func<string, string, string, long, long, string, string> exportMp4,
        Action<string, Guid, long, long, string> registerExport,
        Func<string> createTempSnapshotPath,
        Action<string, byte[]> writeBytes,
        Action<string> deleteFile)
    {
        ArgumentNullException.ThrowIfNull(exportMp4);
        ArgumentNullException.ThrowIfNull(registerExport);
        ArgumentNullException.ThrowIfNull(createTempSnapshotPath);
        ArgumentNullException.ThrowIfNull(writeBytes);
        ArgumentNullException.ThrowIfNull(deleteFile);

        _exportMp4 = exportMp4;
        _registerExport = registerExport;
        _createTempSnapshotPath = createTempSnapshotPath;
        _writeBytes = writeBytes;
        _deleteFile = deleteFile;
    }

    public string ExportMp4(string romPath, string snapshotPath, string inputLogPath, long startFrame, long endFrame, string outputPath) =>
        _exportMp4(romPath, snapshotPath, inputLogPath, startFrame, endFrame, outputPath);

    public void RegisterExport(string romPath, Guid branchId, long startFrame, long endFrame, string exportedPath) =>
        _registerExport(romPath, branchId, startFrame, endFrame, exportedPath);

    public string CreateTempSnapshotPath() => _createTempSnapshotPath();

    public void WriteSnapshotBytes(string path, byte[] bytes) => _writeBytes(path, bytes);

    public void DeleteTemporarySnapshot(string path) => _deleteFile(path);

    internal static string BuildExportObjectName(Guid branchId, long startFrame, long endFrame) =>
        $"exports.branch.{branchId:N}.{startFrame}-{endFrame}.mp4";

    private static void RegisterExportObject(string romPath, Guid branchId, long startFrame, long endFrame, string outputPath)
    {
        RomConfigProfile.RegisterAdditionalObject(
            romPath,
            BuildExportObjectName(branchId, startFrame, endFrame),
            AppObjectStorage.GetObjectKey(ObjectStorageBucket.Saves, outputPath));
    }
}
