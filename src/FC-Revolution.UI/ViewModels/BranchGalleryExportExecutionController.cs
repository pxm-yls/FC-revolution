using System;
using System.Threading.Tasks;

namespace FC_Revolution.UI.ViewModels;

internal sealed class BranchGalleryExportExecutionController
{
    private readonly Func<string?, BranchCanvasNode?> _findCanvasNode;
    private readonly Func<BranchCanvasNode, long, long, Task<string>>? _exportRange;
    private readonly Action<BranchCanvasNode?> _selectNode;
    private readonly Action<string?, string?> _applyExportMarkerIds;
    private readonly Action _notifyExportRangeChanged;
    private readonly Action<string> _updateStatus;

    public BranchGalleryExportExecutionController(
        Func<string?, BranchCanvasNode?> findCanvasNode,
        Func<BranchCanvasNode, long, long, Task<string>>? exportRange,
        Action<BranchCanvasNode?> selectNode,
        Action<string?, string?> applyExportMarkerIds,
        Action notifyExportRangeChanged,
        Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(findCanvasNode);
        ArgumentNullException.ThrowIfNull(selectNode);
        ArgumentNullException.ThrowIfNull(applyExportMarkerIds);
        ArgumentNullException.ThrowIfNull(notifyExportRangeChanged);
        ArgumentNullException.ThrowIfNull(updateStatus);

        _findCanvasNode = findCanvasNode;
        _exportRange = exportRange;
        _selectNode = selectNode;
        _applyExportMarkerIds = applyExportMarkerIds;
        _notifyExportRangeChanged = notifyExportRangeChanged;
        _updateStatus = updateStatus;
    }

    public void ApplyMarkerDecision(BranchGalleryExportMarkerDecision decision)
    {
        if (!decision.ShouldApply)
            return;

        if (decision.SelectedNode != null)
            _selectNode(decision.SelectedNode);

        _applyExportMarkerIds(decision.ExportStartNodeId, decision.ExportEndNodeId);
        _notifyExportRangeChanged();

        if (!string.IsNullOrEmpty(decision.StatusText))
            _updateStatus(decision.StatusText);
    }

    public async Task ExecuteExportAsync(string? exportStartNodeId, string? exportEndNodeId)
    {
        if (_exportRange == null)
            return;

        var request = BranchGalleryExportWorkflowController.BuildExportRequest(
            _findCanvasNode(exportStartNodeId),
            _findCanvasNode(exportEndNodeId));
        if (!request.ShouldExport)
        {
            if (!string.IsNullOrEmpty(request.StatusText))
                _updateStatus(request.StatusText);

            return;
        }

        try
        {
            var outputPath = await _exportRange(request.StartNode!, request.StartFrame, request.EndFrame);
            _updateStatus(BranchGalleryExportWorkflowController.BuildExportSuccessStatus(outputPath));
        }
        catch (Exception ex)
        {
            _updateStatus(BranchGalleryExportWorkflowController.BuildExportFailureStatus(ex.Message));
        }
    }
}
