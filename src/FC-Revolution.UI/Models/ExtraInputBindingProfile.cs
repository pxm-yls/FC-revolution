using System.Collections.Generic;
using Avalonia.Input;

namespace FC_Revolution.UI.Models;

public sealed class ExtraInputBindingProfile
{
    public int Player { get; set; }

    public string Kind { get; set; } = ExtraInputBindingKind.Turbo.ToString();

    public string Key { get; set; } = Avalonia.Input.Key.None.ToString();

    public List<string> Buttons { get; set; } = [];

    public int TurboHz { get; set; } = 10;
}
