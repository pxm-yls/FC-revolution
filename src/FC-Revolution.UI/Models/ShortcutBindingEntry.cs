using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class ShortcutBindingEntry : ObservableObject
{
    private KeyModifiers _pendingModifiers;

    public ShortcutBindingEntry(ShortcutDescriptor descriptor, ShortcutGesture selectedGesture)
    {
        Descriptor = descriptor;
        _selectedGesture = selectedGesture;
    }

    public ShortcutDescriptor Descriptor { get; }

    public string Id => Descriptor.Id;

    public string ScopeLabel => Descriptor.ScopeLabel;

    public string ActionName => Descriptor.ActionName;

    public string Description => Descriptor.Description;

    public ShortcutGesture DefaultGesture => Descriptor.DefaultGesture;

    [ObservableProperty]
    private ShortcutGesture _selectedGesture;

    [ObservableProperty]
    private bool _isCapturing;

    public string SelectedGestureDisplay => IsCapturing
        ? (_pendingModifiers == KeyModifiers.None
            ? "录入中"
            : new ShortcutGesture(Key.None, _pendingModifiers).ToDisplayString())
        : SelectedGesture.ToDisplayString();

    public string CaptureHintText => _pendingModifiers == KeyModifiers.None
        ? "按功能键，或先按 Ctrl / Alt / Shift / Command 再按主键。"
        : $"已记录修饰键 {new ShortcutGesture(Key.None, _pendingModifiers).ToDisplayString()}，请继续按主键。";

    public void BeginCapture()
    {
        _pendingModifiers = KeyModifiers.None;
        IsCapturing = true;
        OnPropertyChanged(nameof(SelectedGestureDisplay));
        OnPropertyChanged(nameof(CaptureHintText));
    }

    public void ContinueCapture(KeyModifiers modifiers)
    {
        _pendingModifiers = ShortcutGesture.NormalizeModifiers(modifiers);
        IsCapturing = true;
        OnPropertyChanged(nameof(SelectedGestureDisplay));
        OnPropertyChanged(nameof(CaptureHintText));
    }

    public void CancelCapture()
    {
        _pendingModifiers = KeyModifiers.None;
        IsCapturing = false;
        OnPropertyChanged(nameof(SelectedGestureDisplay));
        OnPropertyChanged(nameof(CaptureHintText));
    }

    public bool TryBuildGesture(Key key, KeyModifiers modifiers, out ShortcutGesture gesture)
    {
        var preview = ShortcutGesture.CreateCapturePreview(key, modifiers | _pendingModifiers);
        if (ShortcutGesture.IsModifierKey(key))
        {
            ContinueCapture(preview.Modifiers);
            gesture = ShortcutGesture.Empty;
            return false;
        }

        gesture = new ShortcutGesture(key, preview.Modifiers);
        return true;
    }

    public void ApplyGesture(ShortcutGesture gesture)
    {
        SelectedGesture = gesture;
        _pendingModifiers = KeyModifiers.None;
        IsCapturing = false;
        OnPropertyChanged(nameof(SelectedGestureDisplay));
        OnPropertyChanged(nameof(CaptureHintText));
    }

    partial void OnSelectedGestureChanged(ShortcutGesture value) => OnPropertyChanged(nameof(SelectedGestureDisplay));

    partial void OnIsCapturingChanged(bool value)
    {
        if (!value)
            _pendingModifiers = KeyModifiers.None;

        OnPropertyChanged(nameof(SelectedGestureDisplay));
        OnPropertyChanged(nameof(CaptureHintText));
    }
}
