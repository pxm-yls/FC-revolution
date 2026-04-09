using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void UpdatePreviewQueueAggregate()
    {
        PreviewProgress = _previewQueueController.BuildPreviewQueueAggregateProgress(_taskQueue);
    }

    private void AttachTaskQueueMessage(PreviewGenerationTaskItem taskItem)
    {
        taskItem.PropertyChanged -= OnTaskQueueItemPropertyChanged;
        taskItem.PropertyChanged += OnTaskQueueItemPropertyChanged;

        if (!string.IsNullOrWhiteSpace(taskItem.MessageId) &&
            _taskMessageController.Hub.Messages.Any(message => string.Equals(message.Id, taskItem.MessageId, StringComparison.Ordinal)))
        {
            return;
        }

        taskItem.MessageId = null;

        var message = _taskMessageController.Hub.AddMessage(new TaskMessage
        {
            Timestamp = DateTime.UtcNow,
            Category = ResolveTaskQueueMessageCategory(taskItem),
            Title = taskItem.Title,
            Content = taskItem.Status,
            Severity = ResolveTaskQueueMessageSeverity(taskItem),
            Progress = GetTaskQueueMessageProgress(taskItem),
            IsCompleted = taskItem.IsCompleted
        });
        taskItem.MessageId = message.Id;
    }

    private void OnTaskQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PreviewGenerationTaskItem taskItem)
            return;

        SyncTaskQueueMessage(taskItem);
    }

    private void SyncTaskQueueMessage(PreviewGenerationTaskItem taskItem)
    {
        AttachTaskQueueMessage(taskItem);
        if (string.IsNullOrWhiteSpace(taskItem.MessageId))
            return;

        _taskMessageController.Hub.UpdateMessage(
            taskItem.MessageId,
            message =>
            {
                message.Category = ResolveTaskQueueMessageCategory(taskItem);
                message.Title = taskItem.Title;
                message.Content = taskItem.Status;
                message.Severity = ResolveTaskQueueMessageSeverity(taskItem);
                message.Progress = GetTaskQueueMessageProgress(taskItem);
                message.IsCompleted = taskItem.IsCompleted;
            },
            markUnread: taskItem.IsCompleted);
    }

    private static MessageCategory ResolveTaskQueueMessageCategory(PreviewGenerationTaskItem taskItem)
    {
        return taskItem.Status.StartsWith("失败", StringComparison.Ordinal) ||
               taskItem.Status.Contains("失败", StringComparison.Ordinal)
            ? MessageCategory.Error
            : MessageCategory.Task;
    }

    private static MessageSeverity ResolveTaskQueueMessageSeverity(PreviewGenerationTaskItem taskItem)
    {
        if (taskItem.Status.StartsWith("失败", StringComparison.Ordinal) ||
            taskItem.Status.Contains("失败", StringComparison.Ordinal))
        {
            return MessageSeverity.Error;
        }

        if (taskItem.IsCompleted)
            return MessageSeverity.Success;

        return MessageSeverity.Info;
    }

    private static double? GetTaskQueueMessageProgress(PreviewGenerationTaskItem taskItem)
    {
        return taskItem.IsCompleted || taskItem.Progress > 0
            ? taskItem.Progress
            : null;
    }

    private void PruneCompletedTaskQueueItems()
    {
        var completedTasks = _taskQueue.Where(item => item.IsCompleted).ToList();
        foreach (var item in completedTasks)
        {
            item.PropertyChanged -= OnTaskQueueItemPropertyChanged;
            _taskQueue.Remove(item);
        }

        OnPropertyChanged(nameof(HasTaskQueue));
    }

    private PreviewGenerationTaskItem EnqueueImportTask(string title)
    {
        var taskItem = new PreviewGenerationTaskItem(title, "", "导入任务")
        {
            Status = "等待执行",
            Progress = 0
        };
        AttachTaskQueueMessage(taskItem);
        _taskQueue.Add(taskItem);
        OnPropertyChanged(nameof(HasTaskQueue));
        return taskItem;
    }

    private bool HasPendingPreviewJob(string romPath) =>
        _previewQueueController.HasPendingPreviewJob(_taskQueue, romPath, PathsEqual);

    private bool EnqueuePreviewGeneration(RomLibraryItem rom, bool forceRegenerate)
    {
        var admission = _previewQueueController.TryEnqueuePreviewGeneration(_taskQueue, rom, forceRegenerate, PathsEqual);
        if (!admission.Accepted)
        {
            StatusText = admission.StatusText;
            return false;
        }

        var taskItem = admission.TaskItem!;
        AttachTaskQueueMessage(taskItem);
        _taskQueue.Add(taskItem);
        OnPropertyChanged(nameof(HasTaskQueue));
        OnPropertyChanged(nameof(CanGenerateAllPreviews));
        if (admission.PreviewStatusText != null)
            PreviewStatusText = admission.PreviewStatusText;
        StatusText = admission.StatusText;
        _ = ProcessPreviewQueueAsync();
        return true;
    }

    [RelayCommand]
    private async Task RegeneratePreviewForRomAsync(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        CurrentRom = rom;
        EnqueuePreviewGeneration(rom, forceRegenerate: true);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task GeneratePreviewAsync()
    {
        if (CurrentRom == null)
            return;

        EnqueuePreviewGeneration(CurrentRom, forceRegenerate: true);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task GenerateAllPreviewsAsync()
    {
        if (_allRomLibrary.Count == 0)
            return;

        IsSettingsOpen = false;
        var queuedCount = 0;
        foreach (var rom in _allRomLibrary.Where(item => !item.HasPreview))
        {
            if (EnqueuePreviewGeneration(rom, forceRegenerate: false))
                queuedCount++;
        }

        StatusText = queuedCount == 0
            ? "没有新的预览任务加入队列"
            : $"已加入 {queuedCount} 个预览任务";
        PreviewStatusText = queuedCount == 0
            ? "已有预览或已在队列中的 ROM 已自动跳过"
            : $"已加入 {queuedCount} 个 ROM 的预览任务";
        await Task.CompletedTask;
    }

    private async Task ProcessPreviewQueueAsync()
    {
        if (_isPreviewQueueProcessorRunning)
            return;

        _isPreviewQueueProcessorRunning = true;
        IsGeneratingPreview = true;
        OnPropertyChanged(nameof(CanGenerateAllPreviews));

        try
        {
            var activeTasks = new List<Task>();
            while (true)
            {
                while (activeTasks.Count < PreviewGenerationParallelism)
                {
                    var nextJob = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var dispatch = _previewQueueController.TryPrepareNextJob(_taskQueue, _romLibrary, PathsEqual);
                        return dispatch.HasJob && dispatch.Rom != null && dispatch.TaskItem != null
                            ? ((RomLibraryItem Rom, PreviewGenerationTaskItem TaskItem)?)(dispatch.Rom, dispatch.TaskItem)
                            : null;
                    });

                    if (nextJob == null)
                        break;

                    StatusText = _previewQueueController.BuildProcessingStatusText(PreviewGenerationParallelism);
                    activeTasks.Add(RunPreviewJobAsync(nextJob.Value.Rom, nextJob.Value.TaskItem));
                }

                if (activeTasks.Count == 0)
                    break;

                var completedTask = await Task.WhenAny(activeTasks);
                activeTasks.Remove(completedTask);
                await completedTask;
            }

            StatusText = _previewQueueController.BuildCompletionStatusText();
            PreviewStatusText = _previewQueueController.BuildCompletionPreviewStatusText(_taskQueue);
        }
        finally
        {
            _isPreviewQueueProcessorRunning = false;
            IsGeneratingPreview = false;
            OnPropertyChanged(nameof(CanGenerateAllPreviews));
            PruneCompletedTaskQueueItems();
        }
    }

    private async Task RunPreviewJobAsync(RomLibraryItem rom, PreviewGenerationTaskItem taskItem)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                taskItem.Progress = 0;
                taskItem.Status = "生成中 0%";
                UpdatePreviewQueueAggregate();
            });

            Directory.CreateDirectory(GetPreviewDirectory());
            var generatedPreviewPath = GetPreviewPath(rom.Path);
            await GeneratePreviewVideoWithTimeoutAsync(
                rom.Path,
                generatedPreviewPath,
                progress =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        taskItem.Progress = progress;
                        taskItem.Status = $"生成中 {progress:P0}";
                        UpdatePreviewQueueAggregate();
                        PreviewStatusText = $"正在生成: {rom.DisplayName} {progress:P0}";
                    });
                });

            RomConfigProfile.RegisterPreviewVideoObject(
                rom.Path,
                AppObjectStorage.GetObjectKey(ObjectStorageBucket.PreviewVideos, generatedPreviewPath));

            var resolvedPlaybackPath = ResolvePreviewPlaybackPath(rom.Path);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewDebugText = $"[DEBUG] 生成完成\n目标路径: {generatedPreviewPath}\n存在: {File.Exists(generatedPreviewPath)}\n解析路径: {resolvedPlaybackPath}\n一致: {string.Equals(generatedPreviewPath, resolvedPlaybackPath, StringComparison.OrdinalIgnoreCase)}";
                StatusText = $"[DEBUG] 生成完成 存在={File.Exists(generatedPreviewPath)} 路径={Path.GetFileName(generatedPreviewPath)}";

                _previewAssetReadyController.ApplyPreviewAssetReady(
                    rom,
                    resolvedPlaybackPath,
                    ReferenceEquals(CurrentRom, rom),
                    syncCurrentPreviewFrame: true,
                    TryLoadItemPreview,
                    RefreshCurrentRomPreviewAssetState,
                    SyncCurrentPreviewBitmapFromCurrentRom);
                if (_romLibrary.Any(item => item.IsPreviewAnimated))
                    RestartPreviewPlayback();

                taskItem.Complete("已完成");
                UpdatePreviewQueueAggregate();
                PreviewStatusText = $"{rom.DisplayName} 的 {PreviewDurationSeconds} 秒预览已更新";
                StatusText = $"预览已生成: {rom.DisplayName}";
            });

            await PrimeAnimatedPreviewsAsync();
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                taskItem.Complete($"失败: {ex.Message}");
                UpdatePreviewQueueAggregate();
                PreviewStatusText = $"预览生成失败: {ex.Message}";
                StatusText = $"预览生成失败: {ex.Message}";
            });
        }
    }

    private async Task GeneratePreviewVideoWithTimeoutAsync(string romPath, string previewPath, Action<double>? progressCallback = null)
    {
        await _previewGenerationController.GeneratePreviewVideoWithTimeoutAsync(
            romPath,
            previewPath,
            PreviewResolutionScale,
            PreviewGenerationSpeedMultiplier,
            SelectedPreviewEncodingMode,
            progressCallback,
            progressCallback == null
                ? (progress, text) => Dispatcher.UIThread.Post(() =>
                {
                    PreviewProgress = progress;
                    PreviewStatusText = text;
                })
                : null);

        MainWindowPreviewAssetController.TryDeleteSiblingLegacyPreviewFile(previewPath, LegacyPreviewExtension);

        if (progressCallback == null)
            Dispatcher.UIThread.Post(() => PreviewProgress = 0.98);
    }
}
