using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Core;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

public enum BranchLayoutOrientation
{
    Horizontal,
    Vertical,
}

public sealed class BranchCanvasEdge
{
    public Point StartPoint { get; init; }
    public Point EndPoint { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class BranchTimelineTick
{
    public required double X { get; init; }
    public required string FrameLabel { get; init; }
    public required string TimeLabel { get; init; }
}

public sealed class BranchPreviewMarker
{
    public required Guid Id { get; init; }
    public required long Frame { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Diameter { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required WriteableBitmap? Bitmap { get; init; }
}

public sealed class BranchPreviewNode
{
    public required Guid Id { get; init; }
    public required long Frame { get; init; }
    public required double TimestampSeconds { get; init; }
    public required string Title { get; set; }
    public required WriteableBitmap? Bitmap { get; init; }
}

public sealed partial class BranchCanvasNode : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required long Frame { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required WriteableBitmap? Bitmap { get; init; }
    public required bool IsBranchNode { get; init; }
    public required bool IsMainlineNode { get; init; }
    public required string BackgroundHex { get; init; }
    public required string BorderBrushHex { get; init; }
    public required double BorderThicknessValue { get; init; }
    public CoreBranchPoint? BranchPoint { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

public partial class BranchGalleryViewModel : ViewModelBase
{
    private const double NodeWidth = 152;
    private const double NodeHeight = 130;
    private const double DetailPreviewWidth = 272;
    private const double DetailPreviewHeight = 204;

    private readonly ITimeTravelService _timeTravelService;
    private readonly BranchTree _tree;
    private readonly BranchGalleryCanvasProjectionController _canvasProjectionController = new();
    private readonly BranchGalleryCanvasRefreshController _canvasRefreshController;
    private readonly BranchGalleryCanvasApplyController _canvasApplyController;
    private readonly BranchGalleryPreviewNodeWorkflowController _previewNodeWorkflow;
    private readonly BranchGalleryBranchWorkflowController _branchWorkflowController;
    private readonly BranchGallerySelectionEntryController _selectionEntryController;
    private readonly BranchGalleryViewportWorkflowController _viewportWorkflowController;
    private readonly BranchGalleryTimelineNavigationExecutionController _timelineNavigationExecutionController;
    private readonly BranchGalleryExportExecutionController _exportExecutionController;
    private readonly BranchGallerySelectionController _selectionController = new();
    private readonly bool _useCenteredTimelineRail;
    private string? _romPath;
    private uint[]? _lastFrame;
    private BranchLayoutOrientation _orientation = BranchLayoutOrientation.Horizontal;
    private double _zoomFactor = 1.0;
    private string? _selectedNodeId;
    private Guid? _selectedPreviewNodeId;
    private string? _exportStartNodeId;
    private string? _exportEndNodeId;
    private string _statusText = "就绪";
    private double _canvasWidth = 1280;
    private double _canvasHeight = 720;
    private long _currentFrame;
    private double _currentTimestampSeconds;
    private int _axisIntervalSeconds = 30;
    private double _currentMarkerX = 120;
    private int _secondsPer100Pixels = 30;
    private double _mainAxisY = 120;
    private readonly List<BranchPreviewNode> _previewNodes = [];

    public ObservableCollection<BranchCanvasNode> CanvasNodes { get; } = new();
    public ObservableCollection<BranchCanvasEdge> CanvasEdges { get; } = new();
    public ObservableCollection<BranchTimelineTick> AxisTicks { get; } = new();
    public ObservableCollection<BranchPreviewMarker> PreviewMarkers { get; } = new();

    private BranchCanvasNode? _selectedNode;
    private BranchPreviewNode? _selectedPreviewNode;
    public BranchCanvasNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            var decision = _selectionController.BuildNodeSelectionDecision(_selectedNode, value);
            if (decision == null)
                return;

            if (_selectedNode != null)
                _selectedNode.IsSelected = false;

            if (SetProperty(ref _selectedNode, value))
            {
                if (_selectedNode != null)
                    _selectedNode.IsSelected = true;

                _selectedNodeId = decision.SelectedNodeId;
                NotifyProperties(decision.PropertyNamesToNotify);
            }
        }
    }

    public BranchPreviewNode? SelectedPreviewNode
    {
        get => _selectedPreviewNode;
        private set
        {
            var decision = _selectionController.BuildPreviewSelectionDecision(_selectedPreviewNode, value, _selectedNodeId);
            if (decision == null)
                return;

            if (SetProperty(ref _selectedPreviewNode, value))
            {
                _selectedNodeId = decision.SelectedNodeId;
                _selectedPreviewNodeId = decision.SelectedPreviewNodeId;
                NotifyProperties(decision.PropertyNamesToNotify);
            }
        }
    }

    public BranchLayoutOrientation Orientation
    {
        get => _orientation;
        private set
        {
            if (SetProperty(ref _orientation, value))
            {
                OnPropertyChanged(nameof(IsHorizontalLayout));
                OnPropertyChanged(nameof(IsVerticalLayout));
                OnPropertyChanged(nameof(OrientationLabel));
            }
        }
    }

    public double ZoomFactor
    {
        get => _zoomFactor;
        private set
        {
            var clamped = Math.Clamp(value, 0.55, 2.4);
            if (SetProperty(ref _zoomFactor, clamped))
                OnPropertyChanged(nameof(ZoomPercent));
        }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetProperty(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetProperty(ref _canvasHeight, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool HasSelection => _selectionController.HasSelection(_selectedNode, _selectedPreviewNode);

    public bool HasBranchSelection => _selectionController.HasBranchSelection(_selectedNode);

    public bool HasPreviewSelection => _selectionController.HasPreviewSelection(_selectedPreviewNode);

    public bool CanMarkExportRange => _selectionController.CanMarkExportRange(_selectedNode);

    public bool IsHorizontalLayout => Orientation == BranchLayoutOrientation.Horizontal;

    public bool IsVerticalLayout => Orientation == BranchLayoutOrientation.Vertical;

    public string OrientationLabel => IsHorizontalLayout ? "横向时间树" : "纵向时间树";

    public string ZoomPercent => $"{ZoomFactor * 100:F0}%";

    public int AxisIntervalSeconds
    {
        get => _axisIntervalSeconds;
        private set
        {
            var clamped = Math.Clamp(value, 10, 300);
            if (SetProperty(ref _axisIntervalSeconds, clamped))
            {
                OnPropertyChanged(nameof(AxisIntervalLabel));
                OnPropertyChanged(nameof(IsAxisInterval30));
                OnPropertyChanged(nameof(IsAxisInterval60));
                RebuildCanvas();
            }
        }
    }

    public string AxisIntervalLabel => $"轴点 {AxisIntervalSeconds}s";

    public bool IsAxisInterval30 => AxisIntervalSeconds == 30;

    public bool IsAxisInterval60 => AxisIntervalSeconds == 60;

    public double CurrentMarkerX
    {
        get => _currentMarkerX;
        private set => SetProperty(ref _currentMarkerX, value);
    }

    public double MainAxisY
    {
        get => _mainAxisY;
        private set => SetProperty(ref _mainAxisY, value);
    }

    public int SecondsPer100Pixels
    {
        get => _secondsPer100Pixels;
        private set
        {
            if (SetProperty(ref _secondsPer100Pixels, Math.Clamp(value, 15, 480)))
            {
                OnPropertyChanged(nameof(TimeScaleLabel));
                OnPropertyChanged(nameof(IsTimeScale15));
                OnPropertyChanged(nameof(IsTimeScale30));
                OnPropertyChanged(nameof(IsTimeScale60));
                OnPropertyChanged(nameof(IsTimeScale120));
                RebuildCanvas();
            }
        }
    }

    public string TimeScaleLabel => $"时间尺度 {SecondsPer100Pixels}s / 100px";

    public bool IsTimeScale15 => SecondsPer100Pixels == 15;

    public bool IsTimeScale30 => SecondsPer100Pixels == 30;

    public bool IsTimeScale60 => SecondsPer100Pixels == 60;

    public bool IsTimeScale120 => SecondsPer100Pixels == 120;

    public string CurrentMarkerFrameLabel => $"帧 {_currentFrame}";

    public string CurrentMarkerTimeLabel => TimeSpan.FromSeconds(_currentTimestampSeconds).ToString(@"hh\:mm\:ss");

    public string CacheInfo
    {
        get
        {
            var cacheInfo = _timeTravelService.GetCacheInfo();
            return $"热缓存 {cacheInfo.HotCount} 帧 | 温缓存 {cacheInfo.WarmCount} 帧";
        }
    }

    public string SelectedTitle => _selectionController.BuildSelectedTitle(_selectedNode, _selectedPreviewNode);

    public string SelectedSubtitle => _selectionController.BuildSelectedSubtitle(_selectedNode, _selectedPreviewNode);

    public string SelectedMeta => _selectionController.BuildSelectedMeta(_selectedNode, _selectedPreviewNode);

    public string ExportRangeLabel =>
        _selectionController.BuildExportRangeLabel(
            FindCanvasNode(_exportStartNodeId),
            FindCanvasNode(_exportEndNodeId));

    public string EditableBranchName
    {
        get => _selectionController.GetEditableBranchName(_selectedNode);
        set
        {
            if (_selectedNode?.BranchPoint == null)
                return;

            _selectedNode.BranchPoint.Name = value;
            OnPropertyChanged(nameof(EditableBranchName));
            OnPropertyChanged(nameof(SelectedTitle));
        }
    }

    public string EditablePreviewTitle
    {
        get => _selectionController.GetEditablePreviewTitle(_selectedPreviewNode);
        set
        {
            if (_selectedPreviewNode == null)
                return;

            _selectedPreviewNode.Title = value;
            OnPropertyChanged(nameof(EditablePreviewTitle));
            OnPropertyChanged(nameof(SelectedTitle));
            RebuildCanvas();
        }
    }

    public WriteableBitmap? SelectedBitmap => _selectionController.GetSelectedBitmap(_selectedNode, _selectedPreviewNode);

    public BranchGalleryViewModel(
        ITimeTravelService timeTravelService,
        BranchTree tree,
        string? romPath = null,
        uint[]? lastFrame = null,
        Action<CoreBranchPoint, Guid?>? persistBranch = null,
        Action<Guid>? deleteBranch = null,
        Action<CoreBranchPoint>? renameBranch = null,
        Action<Guid>? activateBranch = null,
        Func<BranchCanvasNode, long, long, Task<string>>? exportRange = null,
        Func<BranchCanvasNode, BranchPreviewNode?>? persistPreviewNode = null,
        Action<Guid>? deletePersistedPreviewNode = null,
        Action<Guid, string>? renamePersistedPreviewNode = null,
        Action? notifyTimelineJump = null,
        bool useCenteredTimelineRail = false)
    {
        _timeTravelService = timeTravelService;
        _tree = tree;
        _romPath = romPath;
        _lastFrame = lastFrame;
        _useCenteredTimelineRail = useCenteredTimelineRail;
        _canvasRefreshController = new BranchGalleryCanvasRefreshController(_canvasProjectionController);
        _canvasApplyController = new BranchGalleryCanvasApplyController(
            CanvasNodes,
            CanvasEdges,
            AxisTicks,
            PreviewMarkers,
            _previewNodes,
            mainAxisY => MainAxisY = mainAxisY,
            currentMarkerX => CurrentMarkerX = currentMarkerX,
            canvasWidth => CanvasWidth = canvasWidth,
            canvasHeight => CanvasHeight = canvasHeight,
            node => SelectedNode = node,
            previewNode => SelectedPreviewNode = previewNode);
        _previewNodeWorkflow = new BranchGalleryPreviewNodeWorkflowController(
            node => persistPreviewNode?.Invoke(node) ??
                    _canvasProjectionController.CreatePreviewNodeFromSnapshot(node, _timeTravelService, _romPath),
            previewNodeId => deletePersistedPreviewNode?.Invoke(previewNodeId),
            (previewNodeId, title) => renamePersistedPreviewNode?.Invoke(previewNodeId, title),
            _previewNodes,
            previewNode => SelectedPreviewNode = previewNode,
            status => StatusText = status,
            RebuildCanvas);
        _branchWorkflowController = new BranchGalleryBranchWorkflowController(
            _timeTravelService,
            _tree,
            persistBranch,
            deleteBranch,
            renameBranch,
            node => SelectedNode = node,
            selectedNodeId => _selectedNodeId = selectedNodeId,
            RefreshAll,
            status => StatusText = status);
        _timelineNavigationExecutionController = new BranchGalleryTimelineNavigationExecutionController(
            _timeTravelService,
            branchId => activateBranch?.Invoke(branchId),
            () => notifyTimelineJump?.Invoke(),
            RefreshAll,
            status => StatusText = status);
        _selectionEntryController = new BranchGallerySelectionEntryController(
            node => SelectedNode = node,
            previewNode => SelectedPreviewNode = previewNode,
            previewNodeId => _previewNodes.FirstOrDefault(node => node.Id == previewNodeId),
            ExecuteTimelineNavigation);
        _exportExecutionController = new BranchGalleryExportExecutionController(
            FindCanvasNode,
            exportRange,
            node => SelectedNode = node,
            (startNodeId, endNodeId) =>
            {
                _exportStartNodeId = startNodeId;
                _exportEndNodeId = endNodeId;
            },
            () => OnPropertyChanged(nameof(ExportRangeLabel)),
            status => StatusText = status);
        _viewportWorkflowController = new BranchGalleryViewportWorkflowController(
            () => Orientation,
            orientation => Orientation = orientation,
            () => ZoomFactor,
            zoomFactor => ZoomFactor = zoomFactor,
            () => SecondsPer100Pixels,
            secondsPer100Pixels => SecondsPer100Pixels = secondsPer100Pixels,
            RebuildCanvas,
            _useCenteredTimelineRail);
        RefreshAll();
    }

    public void SetRomPath(string? romPath) => _romPath = romPath;

    public void SetLastFrame(uint[] fb) => _lastFrame = fb;

    public void SetCurrentPosition(long frame, double timestampSeconds)
    {
        _currentFrame = frame;
        _currentTimestampSeconds = timestampSeconds;
        OnPropertyChanged(nameof(CurrentPositionSummary));
        OnPropertyChanged(nameof(CurrentMarkerFrameLabel));
        OnPropertyChanged(nameof(CurrentMarkerTimeLabel));
    }

    public void ReplacePreviewNodes(IEnumerable<BranchPreviewNode> previewNodes)
    {
        _previewNodes.Clear();
        _previewNodes.AddRange(previewNodes.OrderBy(node => node.Frame).ThenBy(node => node.Id));
        RebuildCanvas();
    }

    public string CurrentPositionSummary => $"当前位置: {TimeSpan.FromSeconds(_currentTimestampSeconds):mm\\:ss} / 帧 {_currentFrame}";

    public void RefreshAll()
    {
        RebuildCanvas();
        OnPropertyChanged(nameof(CacheInfo));
        OnPropertyChanged(nameof(CurrentPositionSummary));
    }

    [RelayCommand]
    private void SelectNode(BranchCanvasNode? node) => _selectionEntryController.SelectNode(node);

    [RelayCommand]
    private void SelectPreviewNode(Guid previewNodeId) => _selectionEntryController.SelectPreviewNode(previewNodeId);

    [RelayCommand]
    private void CreateBranch() => _branchWorkflowController.CreateBranch(_lastFrame, _romPath, SelectedNode);

    [RelayCommand]
    private void CreateBranchAtCurrent()
    {
        CreateBranch();
    }

    [RelayCommand]
    private void CreatePreviewNode(BranchCanvasNode? node)
        => _previewNodeWorkflow.CreatePreviewNode(node ?? SelectedNode);

    [RelayCommand]
    private void DeletePreviewNode(Guid previewNodeId)
        => _previewNodeWorkflow.DeletePreviewNode(previewNodeId);

    [RelayCommand]
    private void RenamePreviewNode()
        => _previewNodeWorkflow.RenamePreviewNode(SelectedPreviewNode);

    [RelayCommand]
    private void LoadBranch()
    {
        ExecuteTimelineNavigation(
            BranchGalleryTimelineNavigationController.BuildLoadBranchDecision(SelectedNode));
    }

    [RelayCommand]
    private void SeekToNode(BranchCanvasNode? node)
        => _selectionEntryController.SeekToNode(node);

    [RelayCommand]
    private void DeleteBranch() => _branchWorkflowController.DeleteBranch(SelectedNode);

    [RelayCommand]
    private void RenameBranch() => _branchWorkflowController.RenameBranch(SelectedNode);

    [RelayCommand]
    private void SetExportStart()
    {
        _exportExecutionController.ApplyMarkerDecision(
            BranchGalleryExportWorkflowController.BuildSetStartDecision(SelectedNode, _exportEndNodeId));
    }

    [RelayCommand]
    private void SetExportEnd()
    {
        _exportExecutionController.ApplyMarkerDecision(
            BranchGalleryExportWorkflowController.BuildSetEndDecision(SelectedNode, _exportStartNodeId));
    }

    [RelayCommand]
    private Task ExportRangeAsync() =>
        _exportExecutionController.ExecuteExportAsync(_exportStartNodeId, _exportEndNodeId);

    [RelayCommand]
    private void SetExportStartFromNode(BranchCanvasNode? node)
    {
        _exportExecutionController.ApplyMarkerDecision(
            BranchGalleryExportWorkflowController.BuildSetStartDecision(node, _exportEndNodeId));
    }

    [RelayCommand]
    private void SetExportEndFromNode(BranchCanvasNode? node)
    {
        _exportExecutionController.ApplyMarkerDecision(
            BranchGalleryExportWorkflowController.BuildSetEndDecision(node, _exportStartNodeId));
    }

    [RelayCommand]
    private void Rewind(string framesStr)
    {
        ExecuteTimelineNavigation(
            BranchGalleryTimelineNavigationController.BuildRewindDecision(framesStr));
    }

    [RelayCommand]
    private void ToggleOrientation() => _viewportWorkflowController.ToggleOrientation();

    [RelayCommand]
    private void ZoomIn() => _viewportWorkflowController.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => _viewportWorkflowController.ZoomOut();

    [RelayCommand]
    private void ResetZoom() => _viewportWorkflowController.ResetZoom();

    public void AdjustZoom(double delta) => _viewportWorkflowController.AdjustZoom(delta);

    [RelayCommand]
    private void UseAxisInterval30() => AxisIntervalSeconds = 30;

    [RelayCommand]
    private void UseAxisInterval60() => AxisIntervalSeconds = 60;

    [RelayCommand]
    private void SetTimeScale15() => SecondsPer100Pixels = 15;

    [RelayCommand]
    private void SetTimeScale30() => SecondsPer100Pixels = 30;

    [RelayCommand]
    private void SetTimeScale60() => SecondsPer100Pixels = 60;

    [RelayCommand]
    private void SetTimeScale120() => SecondsPer100Pixels = 120;

    [RelayCommand]
    private void TimelineScaleIn() => AdjustTimeScale(-1);

    [RelayCommand]
    private void TimelineScaleOut() => AdjustTimeScale(1);

    private void AdjustTimeScale(int direction) => _viewportWorkflowController.AdjustTimeScale(direction);

    private void ExecuteTimelineNavigation(BranchGalleryTimelineNavigationDecision decision)
        => _timelineNavigationExecutionController.Execute(decision);

    private BranchCanvasNode? FindCanvasNode(string? nodeId) =>
        nodeId == null ? null : CanvasNodes.FirstOrDefault(node => node.Id == nodeId);

    private void RebuildCanvas()
    {
        var refreshResult = _canvasRefreshController.BuildRefreshResult(
            new BranchGalleryCanvasRefreshRequest(
                _timeTravelService.GetThumbnails(),
                _tree.Roots,
                _previewNodes,
                _romPath,
                Orientation,
                ZoomFactor,
                _useCenteredTimelineRail,
                SecondsPer100Pixels,
                AxisIntervalSeconds,
                _currentTimestampSeconds,
                _selectedNodeId,
                _selectedPreviewNodeId,
                NodeWidth,
                NodeHeight));

        _canvasApplyController.Apply(refreshResult);
    }

    private void NotifyProperties(IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
            OnPropertyChanged(propertyName);
    }
}
