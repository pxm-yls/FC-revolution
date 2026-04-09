using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed record BranchGalleryCanvasProjectionRequest(
    IReadOnlyList<CoreTimelineThumbnail> Timeline,
    IReadOnlyList<CoreBranchPoint> Roots,
    IReadOnlyList<BranchPreviewNode> PreviewNodes,
    BranchLayoutOrientation Orientation,
    double ZoomFactor,
    bool UseCenteredTimelineRail,
    int SecondsPer100Pixels,
    int AxisIntervalSeconds,
    double CurrentTimestampSeconds,
    string? SelectedNodeId,
    double NodeWidth,
    double NodeHeight);

internal sealed record BranchGalleryCanvasProjectionResult(
    IReadOnlyList<BranchCanvasNode> Nodes,
    IReadOnlyList<BranchCanvasEdge> Edges,
    IReadOnlyList<BranchTimelineTick> AxisTicks,
    IReadOnlyList<BranchPreviewMarker> PreviewMarkers,
    double CanvasWidth,
    double CanvasHeight,
    double CurrentMarkerX,
    double MainAxisY);

internal sealed class BranchGalleryCanvasProjectionController
{
    public BranchGalleryCanvasProjectionResult BuildProjection(BranchGalleryCanvasProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nodes = new List<BranchCanvasNode>();
        var edges = new List<BranchCanvasEdge>();
        var ticks = new List<BranchTimelineTick>();
        var previewMarkers = new List<BranchPreviewMarker>();
        var mainlineLookup = new Dictionary<long, (double x, double y)>();

        var pixelsPerSecond = request.UseCenteredTimelineRail ? 100d / request.SecondsPer100Pixels : 12d;
        var logicalCanvasWidth = request.UseCenteredTimelineRail
            ? 1600d
            : Math.Max(1400d, (request.CurrentTimestampSeconds + request.AxisIntervalSeconds) * pixelsPerSecond + 220d);
        var logicalCanvasHeight = request.UseCenteredTimelineRail ? 340d : 720d;
        var leftMargin = request.UseCenteredTimelineRail ? logicalCanvasWidth / 2d : 120d;
        var mainlineAxis = request.UseCenteredTimelineRail ? 158d : 120d;
        var mainAxisY = mainlineAxis * request.ZoomFactor;

        for (var i = 0; i < request.Timeline.Count; i++)
        {
            var frame = request.Timeline[i].Frame;
            var thumb = request.Timeline[i].Thumbnail;
            var seconds = frame / 60d;
            var x = request.UseCenteredTimelineRail
                ? leftMargin + (seconds - request.CurrentTimestampSeconds) * pixelsPerSecond
                : leftMargin + seconds * pixelsPerSecond;
            var y = request.UseCenteredTimelineRail ? mainlineAxis - request.NodeHeight - 18 : mainlineAxis;
            mainlineLookup[frame] = (x, y);

            nodes.Add(CreateNode(
                request,
                id: $"main:{frame}",
                title: $"主线 {i + 1}",
                subtitle: $"帧 {frame}",
                frame: frame,
                createdAt: DateTime.UtcNow,
                x: x,
                y: y,
                bitmap: ThumbnailItem.Create(frame, thumb).Bitmap,
                isBranchNode: false,
                isMainlineNode: true,
                branchPoint: null));

            if (i > 0)
            {
                var prev = mainlineLookup[request.Timeline[i - 1].Frame];
                edges.Add(CreateEdge(request, prev.x, prev.y, x, y, isPrimary: true));
            }
        }

        var upperLane = 1;
        var lowerLane = 1;
        for (var i = 0; i < request.Roots.Count; i++)
        {
            var direction = i % 2 == 0 ? -1 : 1;
            var lane = direction < 0 ? upperLane++ : lowerLane++;
            LayoutBranchRecursive(request, request.Roots[i], direction, lane, 0, mainlineLookup, nodes, edges, mainAxisY);
        }

        if (request.UseCenteredTimelineRail)
        {
            var halfSpanSeconds = logicalCanvasWidth / 2d / pixelsPerSecond;
            var startTick = Math.Max(
                0,
                (int)(Math.Floor((request.CurrentTimestampSeconds - halfSpanSeconds) / request.AxisIntervalSeconds) * request.AxisIntervalSeconds));
            var endTick = (int)Math.Ceiling((request.CurrentTimestampSeconds + halfSpanSeconds) / request.AxisIntervalSeconds) * request.AxisIntervalSeconds;
            for (var tick = startTick; tick <= endTick; tick += request.AxisIntervalSeconds)
            {
                ticks.Add(new BranchTimelineTick
                {
                    X = (leftMargin + (tick - request.CurrentTimestampSeconds) * pixelsPerSecond) * request.ZoomFactor,
                    FrameLabel = $"帧 {(long)Math.Ceiling(tick * 60d)}",
                    TimeLabel = TimeSpan.FromSeconds(tick).ToString(@"hh\:mm\:ss")
                });
            }
        }
        else
        {
            var maxTimelineSeconds = Math.Max(
                request.CurrentTimestampSeconds,
                request.Timeline.Count == 0 ? 0 : request.Timeline.Max(item => item.Frame) / 60d);
            for (var tick = 0; tick <= maxTimelineSeconds + request.AxisIntervalSeconds; tick += request.AxisIntervalSeconds)
            {
                ticks.Add(new BranchTimelineTick
                {
                    X = (leftMargin + tick * pixelsPerSecond) * request.ZoomFactor,
                    FrameLabel = $"帧 {(long)Math.Ceiling(tick * 60d)}",
                    TimeLabel = TimeSpan.FromSeconds(tick).ToString(@"hh\:mm\:ss")
                });
            }
        }

        foreach (var previewNode in request.PreviewNodes.OrderBy(node => node.Frame))
        {
            var previewX = request.UseCenteredTimelineRail
                ? leftMargin + (previewNode.TimestampSeconds - request.CurrentTimestampSeconds) * pixelsPerSecond
                : leftMargin + previewNode.TimestampSeconds * pixelsPerSecond;
            previewMarkers.Add(new BranchPreviewMarker
            {
                Id = previewNode.Id,
                Frame = previewNode.Frame,
                X = previewX * request.ZoomFactor - 13,
                Y = mainlineAxis * request.ZoomFactor - 30,
                Diameter = 26,
                Title = previewNode.Title,
                Subtitle = $"帧 {previewNode.Frame}",
                Bitmap = previewNode.Bitmap
            });
        }

        var canvasWidth = nodes.Count == 0
            ? logicalCanvasWidth * request.ZoomFactor
            : request.UseCenteredTimelineRail
                ? logicalCanvasWidth * request.ZoomFactor
                : Math.Max(1400, nodes.Max(node => node.X) + request.NodeWidth + 160);
        var canvasHeight = nodes.Count == 0
            ? request.UseCenteredTimelineRail ? logicalCanvasHeight * request.ZoomFactor : 320
            : request.UseCenteredTimelineRail
                ? logicalCanvasHeight * request.ZoomFactor
                : Math.Max(320, nodes.Max(node => node.Y) + request.NodeHeight + 120);

        return new BranchGalleryCanvasProjectionResult(
            nodes,
            edges,
            ticks,
            previewMarkers,
            canvasWidth,
            canvasHeight,
            (request.UseCenteredTimelineRail ? leftMargin : leftMargin + request.CurrentTimestampSeconds * pixelsPerSecond) * request.ZoomFactor,
            mainAxisY);
    }

    public BranchPreviewNode? CreatePreviewNodeFromSnapshot(
        BranchCanvasNode node,
        ITimeTravelService timeTravelService,
        string? romPath)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(timeTravelService);

        var snapshot = node.BranchPoint?.Snapshot ?? timeTravelService.GetNearestSnapshot(node.Frame);
        if (snapshot == null || string.IsNullOrWhiteSpace(romPath))
            return null;

        return new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = CoreTimelineModelBridge.ReadFrame(snapshot),
            TimestampSeconds = CoreTimelineModelBridge.ReadTimestampSeconds(snapshot),
            Title = node.Title,
            Bitmap = ThumbnailItem.CreateBitmap(CoreTimelineModelBridge.ReadThumbnail(snapshot), 256, 240)
        };
    }

    private static void LayoutBranchRecursive(
        BranchGalleryCanvasProjectionRequest request,
        CoreBranchPoint point,
        int direction,
        int laneIndex,
        int depth,
        IReadOnlyDictionary<long, (double x, double y)> mainlineLookup,
        ICollection<BranchCanvasNode> nodes,
        ICollection<BranchCanvasEdge> edges,
        double mainAxisY)
    {
        var anchor = FindNearestMainline(mainlineLookup, point.Frame);
        var logicalX = request.UseCenteredTimelineRail ? anchor.x : anchor.x + 140 + depth * 178;
        var logicalY = request.UseCenteredTimelineRail
            ? mainAxisY / request.ZoomFactor + direction * (52 + laneIndex * 96 + depth * 18)
            : 280 + direction * (laneIndex * 190 + depth * 24);

        nodes.Add(CreateNode(
            request,
            id: $"branch:{point.Id}",
            title: point.Name,
            subtitle: $"帧 {point.Frame} • {TimeSpan.FromSeconds(point.TimestampSeconds):mm\\:ss}",
            frame: point.Frame,
            createdAt: point.CreatedAt.ToLocalTime(),
            x: logicalX,
            y: logicalY,
            bitmap: ThumbnailItem.Create(point.Frame, point.Snapshot.Thumbnail).Bitmap,
            isBranchNode: true,
            isMainlineNode: false,
            branchPoint: point));
        edges.Add(CreateEdge(request, anchor.x, anchor.y, logicalX, logicalY, isPrimary: false));

        var childOffset = 0;
        foreach (var child in point.Children.OrderBy(child => child.Frame))
        {
            childOffset++;
            LayoutBranchChildRecursive(
                request,
                child,
                logicalX,
                logicalY,
                direction,
                childOffset,
                depth + 1,
                nodes,
                edges);
        }
    }

    private static void LayoutBranchChildRecursive(
        BranchGalleryCanvasProjectionRequest request,
        CoreBranchPoint childPoint,
        double parentX,
        double parentY,
        int direction,
        int childOffset,
        int depth,
        ICollection<BranchCanvasNode> nodes,
        ICollection<BranchCanvasEdge> edges)
    {
        var logicalX = request.UseCenteredTimelineRail ? parentX : parentX + 182;
        var logicalY = request.UseCenteredTimelineRail
            ? parentY + direction * (childOffset * 84)
            : parentY + direction * (childOffset * 136);

        nodes.Add(CreateNode(
            request,
            id: $"branch:{childPoint.Id}",
            title: childPoint.Name,
            subtitle: $"帧 {childPoint.Frame} • {TimeSpan.FromSeconds(childPoint.TimestampSeconds):mm\\:ss}",
            frame: childPoint.Frame,
            createdAt: childPoint.CreatedAt.ToLocalTime(),
            x: logicalX,
            y: logicalY,
            bitmap: ThumbnailItem.Create(childPoint.Frame, childPoint.Snapshot.Thumbnail).Bitmap,
            isBranchNode: true,
            isMainlineNode: false,
            branchPoint: childPoint));
        edges.Add(CreateEdge(request, parentX, parentY, logicalX, logicalY, isPrimary: false));

        var nestedIndex = 0;
        foreach (var child in childPoint.Children.OrderBy(child => child.Frame))
        {
            nestedIndex++;
            LayoutBranchChildRecursive(
                request,
                child,
                logicalX,
                logicalY,
                direction,
                nestedIndex,
                depth + 1,
                nodes,
                edges);
        }
    }

    private static BranchCanvasNode CreateNode(
        BranchGalleryCanvasProjectionRequest request,
        string id,
        string title,
        string subtitle,
        long frame,
        DateTime createdAt,
        double x,
        double y,
        WriteableBitmap? bitmap,
        bool isBranchNode,
        bool isMainlineNode,
        CoreBranchPoint? branchPoint)
    {
        var zoomedWidth = request.NodeWidth * request.ZoomFactor;
        var zoomedHeight = request.NodeHeight * request.ZoomFactor;
        var zoomedX = x * request.ZoomFactor;
        var zoomedY = y * request.ZoomFactor;

        return request.Orientation == BranchLayoutOrientation.Horizontal
            ? new BranchCanvasNode
            {
                Id = id,
                Title = title,
                Subtitle = subtitle,
                Frame = frame,
                CreatedAt = createdAt,
                X = zoomedX,
                Y = zoomedY,
                Width = zoomedWidth,
                Height = zoomedHeight,
                Bitmap = bitmap,
                IsBranchNode = isBranchNode,
                IsMainlineNode = isMainlineNode,
                BackgroundHex = isBranchNode ? "#312016" : "#241914",
                BorderBrushHex = id == request.SelectedNodeId ? "#F0D2A9" : "#70533D",
                BorderThicknessValue = id == request.SelectedNodeId ? 3 : 1,
                BranchPoint = branchPoint
            }
            : new BranchCanvasNode
            {
                Id = id,
                Title = title,
                Subtitle = subtitle,
                Frame = frame,
                CreatedAt = createdAt,
                X = zoomedY,
                Y = zoomedX,
                Width = zoomedWidth,
                Height = zoomedHeight,
                Bitmap = bitmap,
                IsBranchNode = isBranchNode,
                IsMainlineNode = isMainlineNode,
                BackgroundHex = isBranchNode ? "#312016" : "#241914",
                BorderBrushHex = id == request.SelectedNodeId ? "#F0D2A9" : "#70533D",
                BorderThicknessValue = id == request.SelectedNodeId ? 3 : 1,
                BranchPoint = branchPoint
            };
    }

    private static BranchCanvasEdge CreateEdge(
        BranchGalleryCanvasProjectionRequest request,
        double x1,
        double y1,
        double x2,
        double y2,
        bool isPrimary)
    {
        return request.Orientation == BranchLayoutOrientation.Horizontal
            ? new BranchCanvasEdge
            {
                StartPoint = new Point(CenterX(request, x1), CenterY(request, y1)),
                EndPoint = new Point(CenterX(request, x2), CenterY(request, y2)),
                IsPrimary = isPrimary
            }
            : new BranchCanvasEdge
            {
                StartPoint = new Point(CenterY(request, y1), CenterX(request, x1)),
                EndPoint = new Point(CenterY(request, y2), CenterX(request, x2)),
                IsPrimary = isPrimary
            };
    }

    private static (double x, double y) FindNearestMainline(
        IReadOnlyDictionary<long, (double x, double y)> lookup,
        long frame)
    {
        if (lookup.Count == 0)
            return (120d, 280d);

        var nearest = lookup.Keys.OrderBy(candidate => Math.Abs(candidate - frame)).First();
        return lookup[nearest];
    }

    private static double CenterX(BranchGalleryCanvasProjectionRequest request, double logicalX) =>
        logicalX * request.ZoomFactor + request.NodeWidth * request.ZoomFactor / 2d;

    private static double CenterY(BranchGalleryCanvasProjectionRequest request, double logicalY) =>
        logicalY * request.ZoomFactor + request.NodeHeight * request.ZoomFactor / 2d;
}
