using System;
using System.Text.Json.Serialization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public enum MessageCategory
{
    Status,
    Task,
    Error,
    Debug,
    System
}

public enum MessageSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed partial class TaskMessage : ObservableObject
{
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#F5EBDD"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#7EDC9A"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#F0CF74"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F17C6D"));

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [ObservableProperty]
    private MessageCategory _category = MessageCategory.Status;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private MessageSeverity _severity = MessageSeverity.Info;

    [ObservableProperty]
    private bool _isRead;

    [ObservableProperty]
    private double? _progress;

    [ObservableProperty]
    private bool _isCompleted;

    [JsonIgnore]
    public string CategoryLabel => Category switch
    {
        MessageCategory.Status => "状态",
        MessageCategory.Task => "任务",
        MessageCategory.Error => "错误",
        MessageCategory.Debug => "调试",
        MessageCategory.System => "系统",
        _ => "消息"
    };

    [JsonIgnore]
    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    [JsonIgnore]
    public IBrush SeverityBrush => Severity switch
    {
        MessageSeverity.Success => SuccessBrush,
        MessageSeverity.Warning => WarningBrush,
        MessageSeverity.Error => ErrorBrush,
        _ => InfoBrush
    };

    [JsonIgnore]
    public bool HasUnreadMarker => !IsRead;

    [JsonIgnore]
    public bool HasProgress => Progress.HasValue;

    [JsonIgnore]
    public bool HasDistinctTitle =>
        !string.IsNullOrWhiteSpace(Title) &&
        !string.Equals(Title, CategoryLabel, StringComparison.Ordinal);

    [JsonIgnore]
    public string ProgressPercentText => Progress.HasValue
        ? $"{Math.Clamp(Progress.Value, 0d, 1d):P0}"
        : string.Empty;

    partial void OnCategoryChanged(MessageCategory value)
    {
        OnPropertyChanged(nameof(CategoryLabel));
        OnPropertyChanged(nameof(HasDistinctTitle));
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(HasDistinctTitle));

    partial void OnSeverityChanged(MessageSeverity value) => OnPropertyChanged(nameof(SeverityBrush));

    partial void OnIsReadChanged(bool value) => OnPropertyChanged(nameof(HasUnreadMarker));

    partial void OnProgressChanged(double? value)
    {
        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(ProgressPercentText));
    }
}
