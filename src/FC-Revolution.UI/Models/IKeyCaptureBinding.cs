using Avalonia.Input;

namespace FC_Revolution.UI.Models;

public interface IKeyCaptureBinding
{
    bool IsCapturing { get; set; }

    string SelectedKeyDisplay { get; }

    bool TrySetSelectedKey(Key key);
}
