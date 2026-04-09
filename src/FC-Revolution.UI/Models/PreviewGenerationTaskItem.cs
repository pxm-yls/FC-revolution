using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class PreviewGenerationTaskItem : ObservableObject
{
    public PreviewGenerationTaskItem(string title, string romPath, string category = "")
    {
        Title = title;
        RomPath = romPath;
        Category = category;
        _status = "排队中";
    }

    public string Title { get; }

    public string RomPath { get; }

    public string Category { get; }

    public string? MessageId { get; set; }

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isCompleted;

    public void Complete(string status)
    {
        Status = status;
        Progress = 1;
        IsCompleted = true;
    }
}
