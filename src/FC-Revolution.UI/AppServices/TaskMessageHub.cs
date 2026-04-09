using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class TaskMessageHub : IDisposable
{
    private static readonly object SyncRoot = new();
    private static TaskMessageHub? _instance;

    private readonly TaskMessageStorage _storage;
    private readonly ObservableCollection<TaskMessage> _messages = new();
    private readonly ReadOnlyObservableCollection<TaskMessage> _readonlyMessages;
    private readonly DispatcherTimer _saveTimer;
    private int _suppressItemNotifications;

    private TaskMessageHub(TaskMessageStorage storage)
    {
        _storage = storage;
        _readonlyMessages = new ReadOnlyObservableCollection<TaskMessage>(_messages);
        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };
        _messages.CollectionChanged += (_, e) => MessageCollectionChanged?.Invoke(this, e);

        foreach (var message in _storage.Load())
        {
            Subscribe(message);
            _messages.Add(message);
        }
    }

    public static TaskMessageHub Instance
    {
        get
        {
            lock (SyncRoot)
                return _instance ??= new TaskMessageHub(TaskMessageStorage.CreateDefault());
        }
    }

    public event EventHandler? MessagesChanged;

    public event NotifyCollectionChangedEventHandler? MessageCollectionChanged;

    public ReadOnlyObservableCollection<TaskMessage> Messages => _readonlyMessages;

    public string StoragePath => _storage.StoragePath;

    public TaskMessage AddMessage(TaskMessage message)
    {
        var lastMessage = _messages.LastOrDefault();
        if (lastMessage != null &&
            string.Equals(lastMessage.Title, message.Title, StringComparison.Ordinal) &&
            string.Equals(lastMessage.Content, message.Content, StringComparison.Ordinal) &&
            lastMessage.Category == message.Category &&
            lastMessage.Severity == message.Severity &&
            lastMessage.IsCompleted == message.IsCompleted &&
            Math.Abs((lastMessage.Timestamp - message.Timestamp).TotalMilliseconds) < 500)
        {
            return lastMessage;
        }

        Subscribe(message);
        _messages.Add(message);
        NotifyChanged(immediateSave: message.IsCompleted || message.Severity == MessageSeverity.Error);
        return message;
    }

    public void UpdateMessage(string id, Action<TaskMessage> updateAction, bool markUnread = false)
    {
        var message = _messages.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (message == null)
            return;

        _suppressItemNotifications++;
        try
        {
            updateAction(message);
            if (markUnread)
                message.IsRead = false;
        }
        finally
        {
            _suppressItemNotifications--;
        }

        NotifyChanged(immediateSave: message.IsCompleted || message.Severity == MessageSeverity.Error);
    }

    public void ClearMessages(MessageFilter? filter = null)
    {
        RemoveMessagesInternal(GetMessages(filter).Select(message => message.Id));
    }

    public void ClearMessages(IEnumerable<string> ids)
    {
        RemoveMessagesInternal(ids);
    }

    public List<TaskMessage> GetMessages(MessageFilter? filter = null)
    {
        if (filter == null)
            return [.. _messages.OrderBy(message => message.Timestamp)];

        return [.. _messages.Where(filter.Matches).OrderBy(message => message.Timestamp)];
    }

    public void MarkAllRead(MessageFilter? filter = null)
    {
        IEnumerable<TaskMessage> targets = filter == null
            ? _messages
            : _messages.Where(filter.Matches).ToList();
        var changed = false;

        _suppressItemNotifications++;
        try
        {
            foreach (var message in targets)
            {
                if (message.IsRead)
                    continue;

                message.IsRead = true;
                changed = true;
            }
        }
        finally
        {
            _suppressItemNotifications--;
        }

        if (changed)
            NotifyChanged();
    }

    public string ExportMessages(MessageFilter? filter = null)
    {
        return BuildExportText(GetMessages(filter));
    }

    public void Flush() => SaveNow();

    internal static void ResetForTests(string storagePath)
    {
        lock (SyncRoot)
        {
            _instance?.Dispose();
            _instance = new TaskMessageHub(new TaskMessageStorage(storagePath));
        }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        foreach (var message in _messages)
            message.PropertyChanged -= OnMessagePropertyChanged;
    }

    private void RemoveMessagesInternal(IEnumerable<string> ids)
    {
        var idSet = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (idSet.Count == 0)
            return;

        var removed = false;
        foreach (var message in _messages.Where(item => idSet.Contains(item.Id)).ToList())
        {
            Unsubscribe(message);
            _messages.Remove(message);
            removed = true;
        }

        if (removed)
            NotifyChanged(immediateSave: true);
    }

    private void Subscribe(TaskMessage message)
    {
        message.PropertyChanged -= OnMessagePropertyChanged;
        message.PropertyChanged += OnMessagePropertyChanged;
    }

    private void Unsubscribe(TaskMessage message)
    {
        message.PropertyChanged -= OnMessagePropertyChanged;
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressItemNotifications > 0)
            return;

        NotifyChanged();
    }

    private void NotifyChanged(bool immediateSave = false)
    {
        if (immediateSave)
        {
            _saveTimer.Stop();
            SaveNow();
        }
        else
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        MessagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveNow()
    {
        _storage.Save(_messages);
    }

    private static string BuildExportText(IEnumerable<TaskMessage> messages)
    {
        var items = messages.ToList();
        var builder = new StringBuilder();
        builder.AppendLine("FC-Revolution 消息提醒");
        builder.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"消息总数: {items.Count}");

        if (items.Count == 0)
        {
            builder.AppendLine();
            builder.Append("暂无消息。");
            return builder.ToString();
        }

        foreach (var item in items)
        {
            builder.AppendLine();
            builder.AppendLine($"时间: {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"分类: {item.CategoryLabel}");
            builder.AppendLine($"级别: {item.Severity}");
            if (!string.IsNullOrWhiteSpace(item.Title))
                builder.AppendLine($"标题: {item.Title}");
            builder.AppendLine($"内容: {item.Content}");
            if (item.Progress.HasValue)
                builder.AppendLine($"进度: {item.Progress.Value:P0}");
            builder.AppendLine($"完成: {(item.IsCompleted ? "是" : "否")}");
            builder.AppendLine($"已读: {(item.IsRead ? "是" : "否")}");
        }

        return builder.ToString();
    }
}
