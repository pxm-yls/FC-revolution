using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class InputBindingEntry : ObservableObject, IKeyCaptureBinding
{
    public InputBindingEntry(
        string portId,
        string portLabel,
        string actionId,
        string actionName,
        Key selectedKey,
        IReadOnlyList<Key> availableKeys)
    {
        PortId = portId;
        PortLabel = portLabel;
        ActionId = actionId;
        ActionName = actionName;
        _selectedKey = selectedKey;
        AvailableKeys = availableKeys;
    }

    public string PortId { get; }

    public string PortLabel { get; }

    public string ActionName { get; }

    public string ActionId { get; }

    [ObservableProperty]
    private double _centerX;

    [ObservableProperty]
    private double _centerY;

    [ObservableProperty]
    private double _editorWidth;

    [ObservableProperty]
    private double _editorHeight;

    public double EditorLeft => CenterX - (EditorWidth / 2d);

    public double EditorTop => CenterY - (EditorHeight / 2d);

    public Thickness EditorMargin => new(EditorLeft, EditorTop, 0, 0);

    public IReadOnlyList<Key> AvailableKeys { get; }

    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private bool _isCapturing;

    public string SelectedKeyDisplay => IsCapturing ? "录入" : FormatKey(SelectedKey);

    public bool TrySetSelectedKey(Key key)
    {
        if (!AvailableKeys.Contains(key))
            return false;

        SelectedKey = key;
        IsCapturing = false;
        OnPropertyChanged(nameof(SelectedKeyDisplay));
        return true;
    }

    partial void OnSelectedKeyChanged(Key value) => OnPropertyChanged(nameof(SelectedKeyDisplay));

    partial void OnIsCapturingChanged(bool value) => OnPropertyChanged(nameof(SelectedKeyDisplay));

    partial void OnCenterXChanged(double value)
    {
        OnPropertyChanged(nameof(EditorLeft));
        OnPropertyChanged(nameof(EditorMargin));
    }

    partial void OnCenterYChanged(double value)
    {
        OnPropertyChanged(nameof(EditorTop));
        OnPropertyChanged(nameof(EditorMargin));
    }

    partial void OnEditorWidthChanged(double value)
    {
        OnPropertyChanged(nameof(EditorLeft));
        OnPropertyChanged(nameof(EditorMargin));
    }

    partial void OnEditorHeightChanged(double value)
    {
        OnPropertyChanged(nameof(EditorTop));
        OnPropertyChanged(nameof(EditorMargin));
    }

    public void ApplyLayout(InputBindingLayoutProfile layout)
    {
        var slot = layout.GetSlot(ActionId);
        CenterX = slot.CenterX;
        CenterY = slot.CenterY;
        EditorWidth = slot.Width;
        EditorHeight = slot.Height;
    }

    private static string FormatKey(Key key) => key switch
    {
        Key.LeftCtrl => "LCtrl",
        Key.RightCtrl => "RCtrl",
        Key.LeftShift => "LShift",
        Key.RightShift => "RShift",
        Key.LeftAlt => "LAlt",
        Key.RightAlt => "RAlt",
        Key.Return => "Enter",
        Key.Back => "Back",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        Key.CapsLock => "Caps",
        Key.NumPad0 => "N0",
        Key.NumPad1 => "N1",
        Key.NumPad2 => "N2",
        Key.NumPad3 => "N3",
        Key.NumPad4 => "N4",
        Key.NumPad5 => "N5",
        Key.NumPad6 => "N6",
        Key.NumPad7 => "N7",
        Key.NumPad8 => "N8",
        Key.NumPad9 => "N9",
        _ => key.ToString()
    };
}
