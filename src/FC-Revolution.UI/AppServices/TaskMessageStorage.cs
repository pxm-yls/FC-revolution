using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class TaskMessageStorage
{
    private const int CurrentVersion = 1;
    private const int MaxMessages = 1000;
    private const int MaxAgeDays = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TaskMessageStorage(string storagePath)
    {
        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public static TaskMessageStorage CreateDefault()
    {
        var storageRoot = Path.Combine(
            AppObjectStorage.GetConfigurationsDirectory(),
            "system");

        return new TaskMessageStorage(Path.Combine(storageRoot, "task-messages.json"));
    }

    public List<TaskMessage> Load()
    {
        if (!File.Exists(StoragePath))
            return [];

        try
        {
            using var stream = File.OpenRead(StoragePath);
            var document = JsonSerializer.Deserialize<TaskMessageStorageDocument>(stream, JsonOptions) ?? new TaskMessageStorageDocument();
            var cleanedMessages = Cleanup(document.Messages ?? []);

            if (RequiresRewrite(document, cleanedMessages))
                Save(cleanedMessages);

            return cleanedMessages;
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<TaskMessage> messages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var cleanedMessages = Cleanup(messages);
        var document = new TaskMessageStorageDocument
        {
            Version = CurrentVersion,
            Messages = cleanedMessages,
            LastCleanup = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(StoragePath, json);
    }

    public void Clear()
    {
        if (File.Exists(StoragePath))
            File.Delete(StoragePath);
    }

    private static bool RequiresRewrite(TaskMessageStorageDocument document, IReadOnlyList<TaskMessage> cleanedMessages)
    {
        if (document.Version != CurrentVersion)
            return true;

        if (document.Messages == null)
            return false;

        return document.Messages.Count != cleanedMessages.Count;
    }

    private static List<TaskMessage> Cleanup(IEnumerable<TaskMessage> messages)
    {
        var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);

        return messages
            .Where(message => message.Timestamp >= cutoff)
            .OrderBy(message => message.Timestamp)
            .TakeLast(MaxMessages)
            .Select(Normalize)
            .ToList();
    }

    private static TaskMessage Normalize(TaskMessage message)
    {
        message.Id = string.IsNullOrWhiteSpace(message.Id)
            ? Guid.NewGuid().ToString("N")[..8]
            : message.Id;
        message.Title ??= string.Empty;
        message.Content ??= string.Empty;
        message.Progress = message.Progress.HasValue
            ? Math.Clamp(message.Progress.Value, 0d, 1d)
            : null;

        if (message.Timestamp == default)
            message.Timestamp = DateTime.UtcNow;

        return message;
    }

    private sealed class TaskMessageStorageDocument
    {
        public int Version { get; set; } = CurrentVersion;

        public List<TaskMessage>? Messages { get; set; }

        public DateTime? LastCleanup { get; set; }
    }
}
