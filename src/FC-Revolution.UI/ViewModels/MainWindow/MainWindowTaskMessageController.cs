using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal enum MainWindowLegacyTaskMessageSource
{
    Status,
    Preview,
    Debug,
    System
}

internal sealed class MainWindowTaskMessageController : IDisposable
{
    private readonly TaskMessageHub _taskMessageHub;
    private readonly ObservableCollection<TaskMessage> _filteredMessages = [];
    private string _searchText = string.Empty;
    private MessageCategory? _categoryFilter;
    private bool _isCaptureEnabled = true;
    private bool _isDisposed;

    public MainWindowTaskMessageController(TaskMessageHub taskMessageHub)
    {
        _taskMessageHub = taskMessageHub;
        _taskMessageHub.MessagesChanged += OnTaskMessagesChanged;
        _taskMessageHub.MessageCollectionChanged += OnTaskMessageCollectionChanged;
        RefreshFilteredMessages();
    }

    public event Action? StateChanged;

    public ObservableCollection<TaskMessage> FilteredMessages => _filteredMessages;

    public TaskMessageHub Hub => _taskMessageHub;

    public string SearchText => _searchText;

    public int TaskMessageCount => _taskMessageHub.Messages.Count;

    public int UnreadTaskMessageCount => _taskMessageHub.Messages.Count(message => !message.IsRead);

    public bool HasFilteredTaskMessages => _filteredMessages.Count > 0;

    public bool IsTaskMessageFilterAll => _categoryFilter == null;

    public bool IsTaskMessageFilterStatus => _categoryFilter == MessageCategory.Status;

    public bool IsTaskMessageFilterTask => _categoryFilter == MessageCategory.Task;

    public bool IsTaskMessageFilterError => _categoryFilter == MessageCategory.Error;

    public bool IsTaskMessageFilterDebug => _categoryFilter == MessageCategory.Debug;

    public string TaskMessagePanelSummary => $"共 {TaskMessageCount} 条消息，{UnreadTaskMessageCount} 条未读";

    public bool SetSearchText(string searchText)
    {
        if (string.Equals(_searchText, searchText, StringComparison.Ordinal))
            return false;

        _searchText = searchText;
        RefreshFilteredMessages();
        return true;
    }

    public void SetCategoryFilter(string? category)
    {
        _categoryFilter = category switch
        {
            null or "" or "All" => null,
            nameof(MessageCategory.Status) => MessageCategory.Status,
            nameof(MessageCategory.Task) => MessageCategory.Task,
            nameof(MessageCategory.Error) => MessageCategory.Error,
            nameof(MessageCategory.Debug) => MessageCategory.Debug,
            nameof(MessageCategory.System) => MessageCategory.System,
            _ => null
        };

        RefreshFilteredMessages();
    }

    public void MarkAllRead()
    {
        _taskMessageHub.MarkAllRead();
        NotifyStateChanged();
    }

    public void ClearFilteredMessages()
    {
        var ids = _filteredMessages
            .Select(message => message.Id)
            .ToList();
        _taskMessageHub.ClearMessages(ids);
        RefreshFilteredMessages();
    }

    public IReadOnlyList<TaskMessage> GetFilteredMessagesSnapshot()
    {
        var filter = BuildTaskMessageFilter();
        return _taskMessageHub
            .GetMessages(filter)
            .Where(message => MatchesTaskMessageSearch(message, _searchText))
            .ToList();
    }

    public void PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource source, string content)
    {
        if (!_isCaptureEnabled || string.IsNullOrWhiteSpace(content) || ShouldSuppressLegacyTaskMessage(source, content))
            return;

        _taskMessageHub.AddMessage(new TaskMessage
        {
            Timestamp = DateTime.UtcNow,
            Category = ResolveLegacyTaskMessageCategory(source, content),
            Title = GetLegacyTaskMessageTitle(source, content),
            Content = content,
            Severity = InferTaskMessageSeverity(content),
            IsCompleted = true
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _taskMessageHub.MessagesChanged -= OnTaskMessagesChanged;
        _taskMessageHub.MessageCollectionChanged -= OnTaskMessageCollectionChanged;
    }

    private void OnTaskMessagesChanged(object? sender, EventArgs e)
    {
        if (HasTaskMessageFilter)
            RefreshFilteredMessages();
        else
            NotifyStateChanged();
    }

    private void OnTaskMessageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFilteredMessages();
    }

    private void RefreshFilteredMessages()
    {
        var messages = GetFilteredMessagesSnapshot();
        _filteredMessages.Clear();
        foreach (var message in messages)
            _filteredMessages.Add(message);

        NotifyStateChanged();
    }

    private bool HasTaskMessageFilter =>
        _categoryFilter.HasValue ||
        !string.IsNullOrWhiteSpace(_searchText);

    private MessageFilter? BuildTaskMessageFilter()
    {
        return _categoryFilter.HasValue
            ? new MessageFilter { Category = _categoryFilter.Value }
            : null;
    }

    private static bool MatchesTaskMessageSearch(TaskMessage message, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        return message.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               message.Content.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               message.CategoryLabel.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static MessageCategory ResolveLegacyTaskMessageCategory(MainWindowLegacyTaskMessageSource source, string content)
    {
        if (source == MainWindowLegacyTaskMessageSource.Debug || content.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase))
            return MessageCategory.Debug;

        if (content.Contains("失败", StringComparison.Ordinal) ||
            content.StartsWith("错误", StringComparison.Ordinal) ||
            content.Contains("无效", StringComparison.Ordinal) ||
            content.Contains("不存在", StringComparison.Ordinal))
        {
            return MessageCategory.Error;
        }

        return source switch
        {
            MainWindowLegacyTaskMessageSource.Preview => MessageCategory.Task,
            MainWindowLegacyTaskMessageSource.System => MessageCategory.System,
            _ => MessageCategory.Status
        };
    }

    private static string GetLegacyTaskMessageTitle(MainWindowLegacyTaskMessageSource source, string content)
    {
        if (content.Contains("ROM", StringComparison.OrdinalIgnoreCase))
            return "ROM";

        if (content.Contains("预览", StringComparison.Ordinal))
            return "预览";

        if (content.Contains("局域网", StringComparison.Ordinal))
            return "局域网";

        if (content.Contains("启动", StringComparison.Ordinal) || source == MainWindowLegacyTaskMessageSource.System)
            return "系统";

        return source switch
        {
            MainWindowLegacyTaskMessageSource.Preview => "任务",
            MainWindowLegacyTaskMessageSource.Debug => "调试",
            MainWindowLegacyTaskMessageSource.System => "系统",
            _ => "状态"
        };
    }

    private static bool ShouldSuppressLegacyTaskMessage(MainWindowLegacyTaskMessageSource source, string content)
    {
        if (source == MainWindowLegacyTaskMessageSource.Preview &&
            (content.StartsWith("正在生成:", StringComparison.Ordinal) ||
             content.StartsWith("正在离线生成", StringComparison.Ordinal)))
        {
            return true;
        }

        return content is "尚未生成预览" or "等待生成预览" or "等待开始" or "暂无消息。" ||
               content.StartsWith("已选中:", StringComparison.Ordinal);
    }

    private static MessageSeverity InferTaskMessageSeverity(string content)
    {
        if (content.Contains("失败", StringComparison.Ordinal) ||
            content.StartsWith("错误", StringComparison.Ordinal) ||
            content.Contains("无效", StringComparison.Ordinal) ||
            content.Contains("不存在", StringComparison.Ordinal))
        {
            return MessageSeverity.Error;
        }

        if (content.Contains("警告", StringComparison.Ordinal))
            return MessageSeverity.Warning;

        if (content.Contains("成功", StringComparison.Ordinal) ||
            content.StartsWith("已", StringComparison.Ordinal) ||
            content.Contains("完成", StringComparison.Ordinal))
        {
            return MessageSeverity.Success;
        }

        return MessageSeverity.Info;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
