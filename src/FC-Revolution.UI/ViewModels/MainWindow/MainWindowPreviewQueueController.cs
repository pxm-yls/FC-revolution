using System;
using System.Collections.Generic;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewQueueAdmissionResult(
    bool Accepted,
    PreviewGenerationTaskItem? TaskItem,
    string StatusText,
    string? PreviewStatusText);

internal sealed record PreviewQueueDispatchResult(
    bool HasJob,
    RomLibraryItem? Rom,
    PreviewGenerationTaskItem? TaskItem);

internal sealed class MainWindowPreviewQueueController
{
    private const string PreviewGenerationCategory = "预览生成";
    private const string QueueingStatusPrefix = "排队中";

    public bool HasPendingPreviewJob(
        IReadOnlyCollection<PreviewGenerationTaskItem> taskQueue,
        string romPath,
        Func<string, string, bool> pathsEqual) =>
        taskQueue.Any(item =>
            item.Category == PreviewGenerationCategory &&
            !item.IsCompleted &&
            pathsEqual(item.RomPath, romPath));

    public PreviewQueueAdmissionResult TryEnqueuePreviewGeneration(
        IReadOnlyCollection<PreviewGenerationTaskItem> taskQueue,
        RomLibraryItem rom,
        bool forceRegenerate,
        Func<string, string, bool> pathsEqual)
    {
        if (HasPendingPreviewJob(taskQueue, rom.Path, pathsEqual))
        {
            return new PreviewQueueAdmissionResult(
                false,
                null,
                $"{rom.DisplayName} 的预览任务已在队列中",
                null);
        }

        var taskItem = new PreviewGenerationTaskItem(rom.DisplayName, rom.Path, PreviewGenerationCategory)
        {
            Status = forceRegenerate ? "排队中 · 重新生成" : QueueingStatusPrefix
        };
        return new PreviewQueueAdmissionResult(
            true,
            taskItem,
            $"{rom.DisplayName} 预览任务已加入队列",
            $"{rom.DisplayName} 已加入预览生成队列");
    }

    public PreviewQueueDispatchResult TryPrepareNextJob(
        IReadOnlyList<PreviewGenerationTaskItem> taskQueue,
        IReadOnlyList<RomLibraryItem> romLibrary,
        Func<string, string, bool> pathsEqual)
    {
        var taskItem = taskQueue.FirstOrDefault(item =>
            item.Category == PreviewGenerationCategory &&
            !item.IsCompleted &&
            item.Status.StartsWith(QueueingStatusPrefix, StringComparison.Ordinal));
        if (taskItem == null)
            return new PreviewQueueDispatchResult(false, null, null);

        var rom = romLibrary.FirstOrDefault(item => pathsEqual(item.Path, taskItem.RomPath));
        if (rom == null)
        {
            taskItem.Status = "失败: ROM 不存在";
            taskItem.IsCompleted = true;
            return new PreviewQueueDispatchResult(false, null, null);
        }

        taskItem.Status = "等待执行";
        return new PreviewQueueDispatchResult(true, rom, taskItem);
    }

    public string BuildProcessingStatusText(int previewGenerationParallelism) =>
        $"预览队列执行中，并行度 {previewGenerationParallelism}";

    public string BuildCompletionStatusText() => "预览队列已处理完成";

    public string BuildCompletionPreviewStatusText(IReadOnlyCollection<PreviewGenerationTaskItem> taskQueue) =>
        taskQueue.Any(item => item.Category == PreviewGenerationCategory && !item.IsCompleted)
            ? "仍有预览任务等待处理"
            : "全部预览任务已完成";

    public double BuildPreviewQueueAggregateProgress(IReadOnlyCollection<PreviewGenerationTaskItem> taskQueue)
    {
        var previewTasks = taskQueue.Where(item => item.Category == PreviewGenerationCategory).ToList();
        return previewTasks.Count == 0
            ? 0
            : previewTasks.Average(item => item.Progress);
    }
}
