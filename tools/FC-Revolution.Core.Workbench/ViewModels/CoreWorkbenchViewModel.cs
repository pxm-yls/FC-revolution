using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;

namespace FCRevolution.Core.Workbench.ViewModels;

public sealed partial class CoreWorkbenchViewModel : ObservableObject
{
    private const int DefaultFramesToRun = 2;
    private readonly Func<CoreRuntimeWorkspaceOptions, CoreRuntimeWorkspace> _createWorkspace;
    private readonly Func<ManagedCoreRuntimeOptions, string?, CorePreviewFrameCaptureService> _createPreviewService;

    public CoreWorkbenchViewModel(
        Func<CoreRuntimeWorkspaceOptions, CoreRuntimeWorkspace>? createWorkspace = null,
        Func<ManagedCoreRuntimeOptions, string?, CorePreviewFrameCaptureService>? createPreviewService = null)
    {
        _createWorkspace = createWorkspace ?? CoreRuntimeWorkspace.Create;
        _createPreviewService = createPreviewService ?? CreatePreviewService;
        ResourceRootPath = AppObjectStorage.GetResourceRoot();
    }

    public ObservableCollection<CoreWorkbenchCatalogEntryViewModel> CatalogEntries { get; } = [];

    [ObservableProperty]
    private string resourceRootPath = string.Empty;

    [ObservableProperty]
    private string probeDirectoriesText = string.Empty;

    [ObservableProperty]
    private string packagePath = string.Empty;

    [ObservableProperty]
    private string selectedCoreId = string.Empty;

    [ObservableProperty]
    private string romPath = string.Empty;

    [ObservableProperty]
    private string framesToRunText = DefaultFramesToRun.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string statusText = "Ready. Configure resource root, probe paths, or a package path to begin.";

    [ObservableProperty]
    private string smokeSummary = "No smoke check executed yet.";

    [ObservableProperty]
    private string selectedEntrySummary = "No core selected.";

    [ObservableProperty]
    private string previewSummary = "No preview captured yet.";

    [ObservableProperty]
    private WriteableBitmap? previewBitmap;

    [ObservableProperty]
    private CoreWorkbenchCatalogEntryViewModel? selectedEntry;

    partial void OnSelectedEntryChanged(CoreWorkbenchCatalogEntryViewModel? value)
    {
        if (value is not null)
        {
            SelectedCoreId = value.CoreId;
            SelectedEntrySummary = value.Summary;
        }
        else
        {
            SelectedEntrySummary = "No core selected.";
        }
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        StatusText = "Refreshing catalog...";

        try
        {
            var previousCoreId = SelectedEntry?.CoreId ?? SelectedCoreId;
            var result = await Task.Run(() =>
            {
                using var workspace = _createWorkspace(BuildWorkspaceOptions());
                var entries = ManagedCoreRuntime.LoadCatalogEntries(workspace.RuntimeOptions)
                    .Select(static entry => new CoreWorkbenchCatalogEntryViewModel(entry))
                    .ToList();
                return new RefreshCatalogResult(workspace.ResourceRootPath, entries);
            });

            CatalogEntries.Clear();
            foreach (var entry in result.Entries)
                CatalogEntries.Add(entry);

            SelectedEntry = CatalogEntries.FirstOrDefault(entry =>
                string.Equals(entry.CoreId, previousCoreId, StringComparison.OrdinalIgnoreCase))
                ?? CatalogEntries.FirstOrDefault();

            if (SelectedEntry is null && !string.IsNullOrWhiteSpace(previousCoreId))
                SelectedCoreId = previousCoreId;

            StatusText = $"Catalog refreshed. resourceRoot={result.ResourceRootPath}; entries={result.Entries.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Catalog refresh failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunSmokeCheckAsync()
    {
        StatusText = "Running smoke check...";

        if (!TryParseFramesToRun(out var framesToRun, out var frameError))
        {
            StatusText = frameError;
            return;
        }

        try
        {
            var requestedCoreId = string.IsNullOrWhiteSpace(SelectedCoreId) ? null : SelectedCoreId.Trim();
            var romPath = string.IsNullOrWhiteSpace(RomPath) ? null : RomPath.Trim();

            var result = await Task.Run(() =>
            {
                using var workspace = _createWorkspace(BuildWorkspaceOptions());
                var smokeResult = CoreSessionSmokeTester.Run(new CoreSessionSmokeTestRequest(
                    CoreId: requestedCoreId,
                    MediaPath: romPath,
                    FramesToRun: framesToRun,
                    RuntimeOptions: workspace.RuntimeOptions));
                return new SmokeCheckResult(workspace.ResourceRootPath, smokeResult);
            });

            SmokeSummary = BuildSmokeSummary(result.ResourceRootPath, result.SmokeResult);
            StatusText = result.SmokeResult.Succeeded
                ? "Smoke check passed."
                : $"Smoke check completed with failure: {result.SmokeResult.FailureMessage ?? "unknown error"}";
        }
        catch (Exception ex)
        {
            StatusText = $"Smoke check failed to execute: {ex.Message}";
            SmokeSummary = ex.ToString();
        }
    }

    [RelayCommand]
    private async Task CapturePreviewAsync()
    {
        StatusText = "Capturing preview frame...";
        PreviewBitmap = null;

        if (string.IsNullOrWhiteSpace(RomPath))
        {
            StatusText = "Preview capture requires a ROM / media path.";
            return;
        }

        try
        {
            var requestedCoreId = string.IsNullOrWhiteSpace(SelectedCoreId) ? null : SelectedCoreId.Trim();
            var romPath = RomPath.Trim();
            var result = await Task.Run(() =>
            {
                using var workspace = _createWorkspace(BuildWorkspaceOptions());
                var previewService = _createPreviewService(workspace.RuntimeOptions, requestedCoreId);
                WriteableBitmap? previewBitmap = null;
                var previewResult = previewService.Capture(
                    new CorePreviewFrameCaptureRequest(
                        MediaPath: romPath,
                        TotalFrames: 1,
                        CaptureStride: 1,
                        MaxCapturedFrames: 1,
                        ExpectedWidth: 256,
                        ExpectedHeight: 240,
                        TargetRunFps: 60),
                    capturedFrame => previewBitmap = CreateBitmap(capturedFrame.Frame.Pixels, capturedFrame.Frame.Width, capturedFrame.Frame.Height));
                return new PreviewCaptureResult(workspace.ResourceRootPath, previewResult, previewBitmap);
            });

            PreviewBitmap = result.PreviewBitmap;
            PreviewSummary = BuildPreviewSummary(result.ResourceRootPath, result.PreviewResult);
            StatusText = "Preview captured.";
        }
        catch (Exception ex)
        {
            StatusText = $"Preview capture failed: {ex.Message}";
            PreviewSummary = ex.ToString();
            PreviewBitmap = null;
        }
    }

    private CoreRuntimeWorkspaceOptions BuildWorkspaceOptions() => new(
        ResourceRootPath: string.IsNullOrWhiteSpace(ResourceRootPath) ? null : ResourceRootPath.Trim(),
        ProbeDirectories: ParseProbeDirectories(),
        PackagePath: string.IsNullOrWhiteSpace(PackagePath) ? null : PackagePath.Trim());

    private static CorePreviewFrameCaptureService CreatePreviewService(
        ManagedCoreRuntimeOptions runtimeOptions,
        string? coreId) =>
        new(() =>
            ManagedCoreRuntime.TryCreateSession(
                new CoreSessionLaunchRequest(coreId),
                out var session,
                defaultCoreId: coreId,
                options: runtimeOptions)
                ? session!
                : ManagedCoreRuntime.CreateUnavailableSession(coreId));

    private IReadOnlyList<string> ParseProbeDirectories() =>
        ProbeDirectoriesText
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private bool TryParseFramesToRun(out int? framesToRun, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(FramesToRunText))
        {
            framesToRun = null;
            errorMessage = string.Empty;
            return true;
        }

        if (int.TryParse(FramesToRunText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= 0)
        {
            framesToRun = parsed;
            errorMessage = string.Empty;
            return true;
        }

        framesToRun = null;
        errorMessage = "Frames To Run must be a non-negative integer.";
        return false;
    }

    private static string BuildSmokeSummary(string resourceRootPath, CoreSessionSmokeTestResult smokeResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Resource Root: {resourceRootPath}");
        builder.AppendLine($"Requested Core: {smokeResult.RequestedCoreId ?? "(default)"}");
        builder.AppendLine($"Selected Core: {smokeResult.SelectedCoreId ?? "(none)"}");
        builder.AppendLine($"Session Created: {smokeResult.SessionCreated}");
        builder.AppendLine($"Succeeded: {smokeResult.Succeeded}");

        if (smokeResult.RuntimeInfo is not null)
        {
            builder.AppendLine(
                $"Runtime: {smokeResult.RuntimeInfo.DisplayName} | system={smokeResult.RuntimeInfo.SystemId} | version={smokeResult.RuntimeInfo.Version} | binary={smokeResult.RuntimeInfo.BinaryKind}");
        }

        builder.AppendLine(
            $"Input Schema: ports={smokeResult.InputPortCount} | actions={smokeResult.InputActionCount}");
        builder.AppendLine(
            $"Capabilities: {(smokeResult.CapabilityIds.Count == 0 ? "(none)" : string.Join(", ", smokeResult.CapabilityIds.OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)))}");

        if (smokeResult.LoadResult is not null)
        {
            builder.AppendLine(
                $"Media Load: {(smokeResult.LoadResult.Success ? "ok" : "failed")} | {smokeResult.LoadResult.ErrorMessage ?? "(none)"}");
        }

        if (smokeResult.StepResults.Count > 0)
        {
            builder.AppendLine($"Frames Executed: {smokeResult.StepResults.Count}");
            for (var index = 0; index < smokeResult.StepResults.Count; index++)
            {
                var step = smokeResult.StepResults[index];
                builder.AppendLine(
                    $"  frame[{index}]: {(step.Success ? "ok" : "failed")} | presentation={step.PresentationIndex} | {step.ErrorMessage ?? "(none)"}");
            }
        }

        builder.AppendLine($"Video Frames Observed: {smokeResult.VideoFrameCount}");
        builder.AppendLine($"Failure Message: {smokeResult.FailureMessage ?? "(none)"}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildPreviewSummary(string resourceRootPath, CorePreviewFrameCaptureResult previewResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Resource Root: {resourceRootPath}");
        builder.AppendLine($"Generated Frames: {previewResult.GeneratedFrames}");
        builder.AppendLine($"Captured Frames: {previewResult.CapturedFrames}");
        builder.AppendLine($"Elapsed: {previewResult.Elapsed}");
        return builder.ToString().TrimEnd();
    }

    private static WriteableBitmap CreateBitmap(uint[] frameBuffer, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var locked = bitmap.Lock();
        unsafe
        {
            fixed (uint* source = frameBuffer)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)locked.Address,
                    (long)locked.RowBytes * height,
                    (long)frameBuffer.Length * sizeof(uint));
            }
        }

        return bitmap;
    }

    private sealed record RefreshCatalogResult(
        string ResourceRootPath,
        IReadOnlyList<CoreWorkbenchCatalogEntryViewModel> Entries);

    private sealed record SmokeCheckResult(
        string ResourceRootPath,
        CoreSessionSmokeTestResult SmokeResult);

    private sealed record PreviewCaptureResult(
        string ResourceRootPath,
        CorePreviewFrameCaptureResult PreviewResult,
        WriteableBitmap? PreviewBitmap);
}
