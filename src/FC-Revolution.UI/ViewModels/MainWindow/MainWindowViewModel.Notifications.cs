using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private static readonly MainWindowStartupProgressController StartupProgressController = new();

    public double TaskMessagePanelWidth => 380;

    public bool IsTaskMessagePanelVisible
    {
        get => _isTaskMessagePanelVisible;
        private set => SetProperty(ref _isTaskMessagePanelVisible, value);
    }

    public bool HasFilteredTaskMessages => _taskMessageController.HasFilteredTaskMessages;

    public bool NoFilteredTaskMessages => !HasFilteredTaskMessages;

    public int TaskMessageCount => _taskMessageController.TaskMessageCount;

    public int UnreadTaskMessageCount => _taskMessageController.UnreadTaskMessageCount;

    public string TaskMessagePanelSummary => _taskMessageController.TaskMessagePanelSummary;

    public bool IsTaskMessageFilterAll => _taskMessageController.IsTaskMessageFilterAll;

    public bool IsTaskMessageFilterStatus => _taskMessageController.IsTaskMessageFilterStatus;

    public bool IsTaskMessageFilterTask => _taskMessageController.IsTaskMessageFilterTask;

    public bool IsTaskMessageFilterError => _taskMessageController.IsTaskMessageFilterError;

    public bool IsTaskMessageFilterDebug => _taskMessageController.IsTaskMessageFilterDebug;

    public string TaskMessageSearchText
    {
        get => _taskMessageController.SearchText;
        set
        {
            if (!_taskMessageController.SetSearchText(value))
                return;

            OnPropertyChanged(nameof(TaskMessageSearchText));
        }
    }

    public string TaskMessageShortcutLabel => GetShortcutDisplay(ShortcutCatalog.MainShowTaskMessages);

    public string TaskMessagePanelTitle => $"消息提醒 ({TaskMessageShortcutLabel})";

    public string MemoryDiagnosticsShortcutLabel => GetShortcutDisplay(ShortcutCatalog.MainToggleMemoryDiagnostics);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
                _taskMessageController.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.Status, value);
        }
    }

    public string StartupDiagnosticsLogPath => StartupDiagnostics.LogPath;

    public string StartupDiagnosticsText
    {
        get => _startupDiagnosticsText;
        private set => SetProperty(ref _startupDiagnosticsText, value);
    }

    public bool IsStartupProgressVisible
    {
        get => _isStartupProgressVisible;
        private set => SetProperty(ref _isStartupProgressVisible, value);
    }

    public string StartupProgressText => BuildStartupProgressText();

    public string PreviewStatusText
    {
        get => _previewStatusText;
        private set
        {
            if (SetProperty(ref _previewStatusText, value))
            {
                OnPropertyChanged(nameof(CurrentRomActionSummary));
                _taskMessageController.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.Preview, value);
            }
        }
    }

    public string PreviewDebugText
    {
        get => _previewDebugText;
        private set
        {
            if (SetProperty(ref _previewDebugText, value))
                _taskMessageController.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.Debug, value);
        }
    }

    private void RefreshTaskMessageSummary()
    {
        OnPropertyChanged(nameof(HasFilteredTaskMessages));
        OnPropertyChanged(nameof(NoFilteredTaskMessages));
        OnPropertyChanged(nameof(TaskMessageCount));
        OnPropertyChanged(nameof(UnreadTaskMessageCount));
        OnPropertyChanged(nameof(TaskMessagePanelSummary));
        OnPropertyChanged(nameof(IsTaskMessageFilterAll));
        OnPropertyChanged(nameof(IsTaskMessageFilterStatus));
        OnPropertyChanged(nameof(IsTaskMessageFilterTask));
        OnPropertyChanged(nameof(IsTaskMessageFilterError));
        OnPropertyChanged(nameof(IsTaskMessageFilterDebug));
    }

    public void ShowTaskMessagePanel()
    {
        IsTaskMessagePanelVisible = true;
        RefreshTaskMessageSummary();
    }

    public void HideTaskMessagePanel()
    {
        IsTaskMessagePanelVisible = false;
    }

    private string BuildStartupProgressText()
    {
        var state = new MainWindowStartupProgressState(
            _startupCurrentStepText,
            _startupGameListStatusText,
            _startupPreviewStatusText,
            _startupLanStatusText,
            IsStartupProgressVisible);
        return StartupProgressController.BuildProgressText(state);
    }

    private void UpdateStartupStep(
        string currentStep,
        string? gameListStatus = null,
        string? previewStatus = null,
        string? lanStatus = null,
        bool? isVisible = null)
    {
        var currentState = new MainWindowStartupProgressState(
            _startupCurrentStepText,
            _startupGameListStatusText,
            _startupPreviewStatusText,
            _startupLanStatusText,
            IsStartupProgressVisible);
        var update = StartupProgressController.Update(
            currentState,
            currentStep,
            gameListStatus,
            previewStatus,
            lanStatus,
            isVisible);
        if (update.Changed)
        {
            IsStartupProgressVisible = update.State.IsVisible;
            _startupCurrentStepText = update.State.CurrentStep;
            _startupGameListStatusText = update.State.GameListStatus;
            _startupPreviewStatusText = update.State.PreviewStatus;
            _startupLanStatusText = update.State.LanStatus;
            OnPropertyChanged(nameof(StartupProgressText));
            if (update.CurrentStepChanged)
                _taskMessageController.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.System, currentStep);
        }
    }

    private async Task HideStartupProgressAsync()
    {
        await Task.Delay(1000);
        if (_isShuttingDown)
            return;

        IsStartupProgressVisible = false;
    }

    private string BuildTaskMessageExportText()
    {
        var messages = _taskMessageController.GetFilteredMessagesSnapshot();

        var builder = new StringBuilder();
        builder.AppendLine("FC-Revolution 消息提醒");
        builder.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"消息总数: {messages.Count}");

        if (messages.Count == 0)
        {
            builder.AppendLine();
            builder.Append("暂无消息。");
            return builder.ToString();
        }

        foreach (var message in messages)
        {
            builder.AppendLine();
            builder.AppendLine($"时间: {message.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"分类: {message.CategoryLabel}");
            builder.AppendLine($"级别: {message.Severity}");
            if (!string.IsNullOrWhiteSpace(message.Title))
                builder.AppendLine($"标题: {message.Title}");
            builder.AppendLine($"内容: {message.Content}");
            if (message.Progress.HasValue)
                builder.AppendLine($"进度: {message.Progress.Value:P0}");
            builder.AppendLine($"完成: {(message.IsCompleted ? "是" : "否")}");
            builder.AppendLine($"已读: {(message.IsRead ? "是" : "否")}");
        }

        return builder.ToString();
    }

    private async Task CopyTextToClipboardAsync(string text, string successStatus)
    {
        var topLevel = GetDesktopMainWindow();
        if (topLevel?.Clipboard is not { } clipboard)
            return;

        await clipboard.SetTextAsync(text);
        StatusText = successStatus;
    }

    private async Task ExportTextAsync(string title, string suggestedFileName, string text, string successStatus)
    {
        var topLevel = GetDesktopMainWindow();
        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "txt",
            ShowOverwritePrompt = true,
            FileTypeChoices = NotificationExportFileTypes
        });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(text);
        await writer.FlushAsync();

        var exportPath = file.TryGetLocalPath();
        StatusText = string.IsNullOrWhiteSpace(exportPath)
            ? successStatus
            : $"{successStatus}: {Path.GetFileName(exportPath)}";
    }

    [RelayCommand]
    private void CloseTaskMessagePanel() => HideTaskMessagePanel();

    [RelayCommand]
    private void MarkAllTaskMessagesRead()
    {
        _taskMessageController.MarkAllRead();
    }

    [RelayCommand]
    private void ClearTaskMessages()
    {
        _taskMessageController.ClearFilteredMessages();
    }

    [RelayCommand]
    private Task ExportTaskMessagesAsync() => ExportTextAsync(
        "导出消息提醒",
        $"fc-revolution-task-messages-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        BuildTaskMessageExportText(),
        "消息提醒已导出");

    [RelayCommand]
    private void SetTaskMessageCategoryFilter(string? category)
    {
        _taskMessageController.SetCategoryFilter(category);
    }

    [RelayCommand]
    private void ReloadStartupDiagnostics()
    {
        RefreshStartupDiagnosticsSnapshot();
        LogStartup("startup diagnostics panel refreshed");
    }

    private void OnStartupDiagnosticsUpdated()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshStartupDiagnosticsSnapshot);
        }
        catch
        {
            RefreshStartupDiagnosticsSnapshot();
        }
    }

    private void RefreshStartupDiagnosticsSnapshot()
    {
        StartupDiagnosticsText = StartupDiagnostics.ReadRecentLog();
        OnPropertyChanged(nameof(StartupDiagnosticsLogPath));
    }

    private void LogStartup(string message)
    {
        StartupDiagnostics.Write("main-vm", message);
        RefreshStartupDiagnosticsSnapshot();
    }
}
