using System;
using System.Threading.Tasks;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowBranchExportExecutionController
{
    private readonly MainWindowBranchExportRuntimeHost _runtimeHost;
    private readonly Func<Func<string>, Task<string>> _runInBackground;

    public MainWindowBranchExportExecutionController()
        : this(
            new MainWindowBranchExportRuntimeHost(),
            work => Task.Run(work))
    {
    }

    internal MainWindowBranchExportExecutionController(
        MainWindowBranchExportRuntimeHost runtimeHost,
        Func<Func<string>, Task<string>> runInBackground)
    {
        ArgumentNullException.ThrowIfNull(runtimeHost);
        ArgumentNullException.ThrowIfNull(runInBackground);

        _runtimeHost = runtimeHost;
        _runInBackground = runInBackground;
    }

    public Task<string> ExecuteAsync(MainWindowBranchExportPlan plan, long startFrame, long endFrame) =>
        _runInBackground(() => Execute(plan, startFrame, endFrame));

    public string Execute(MainWindowBranchExportPlan plan, long startFrame, long endFrame)
    {
        if (plan.SnapshotPath != null)
            return ExportWithSnapshotPath(plan, startFrame, endFrame);

        return ExportWithTemporarySnapshot(plan, startFrame, endFrame);
    }

    internal static string BuildExportObjectName(Guid branchId, long startFrame, long endFrame) =>
        MainWindowBranchExportRuntimeHost.BuildExportObjectName(branchId, startFrame, endFrame);

    private string ExportWithSnapshotPath(MainWindowBranchExportPlan plan, long startFrame, long endFrame)
    {
        var exportedPath = _runtimeHost.ExportMp4(
            plan.RomPath,
            plan.SnapshotPath!,
            plan.InputLogPath,
            startFrame,
            endFrame,
            plan.OutputPath);

        _runtimeHost.RegisterExport(plan.RomPath, plan.BranchId, startFrame, endFrame, exportedPath);
        return exportedPath;
    }

    private string ExportWithTemporarySnapshot(MainWindowBranchExportPlan plan, long startFrame, long endFrame)
    {
        var tempPath = _runtimeHost.CreateTempSnapshotPath();

        try
        {
            _runtimeHost.WriteSnapshotBytes(tempPath, plan.SnapshotBytes ?? Array.Empty<byte>());

            var exportedPath = _runtimeHost.ExportMp4(
                plan.RomPath,
                tempPath,
                plan.InputLogPath,
                startFrame,
                endFrame,
                plan.OutputPath);
            _runtimeHost.RegisterExport(plan.RomPath, plan.BranchId, startFrame, endFrame, exportedPath);
            return exportedPath;
        }
        finally
        {
            _runtimeHost.DeleteTemporarySnapshot(tempPath);
        }
    }
}
