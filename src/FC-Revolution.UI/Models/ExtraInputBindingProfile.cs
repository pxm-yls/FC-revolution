using System.Collections.Generic;
using Avalonia.Input;

namespace FC_Revolution.UI.Models;

public sealed class ExtraInputBindingProfile
{
    private int _legacyPortOrdinal = -1;

    public string PortId { get; set; } = string.Empty;

    public int LegacyPortOrdinal
    {
        get => _legacyPortOrdinal;
        set => _legacyPortOrdinal = value;
    }

    public int Player
    {
        get => _legacyPortOrdinal;
        set => _legacyPortOrdinal = value;
    }

    public string Kind { get; set; } = ExtraInputBindingKind.Turbo.ToString();

    public string Key { get; set; } = Avalonia.Input.Key.None.ToString();

    public List<string> Buttons { get; set; } = [];

    public int TurboHz { get; set; } = 10;
}
