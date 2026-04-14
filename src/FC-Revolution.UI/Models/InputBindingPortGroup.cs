using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FC_Revolution.UI.Models;

public sealed class InputBindingPortGroup
{
    public InputBindingPortGroup(
        string portId,
        string portLabel,
        IEnumerable<InputBindingEntry>? inputBindings = null,
        IEnumerable<ExtraInputBindingEntry>? extraBindings = null)
    {
        PortId = portId;
        PortLabel = portLabel;
        InputBindings = inputBindings == null
            ? new ObservableCollection<InputBindingEntry>()
            : new ObservableCollection<InputBindingEntry>(inputBindings);
        ExtraBindings = extraBindings == null
            ? new ObservableCollection<ExtraInputBindingEntry>()
            : new ObservableCollection<ExtraInputBindingEntry>(extraBindings);
    }

    public string PortId { get; }

    public string PortLabel { get; }

    public ObservableCollection<InputBindingEntry> InputBindings { get; }

    public ObservableCollection<ExtraInputBindingEntry> ExtraBindings { get; }

    public bool HasExtraBindings => ExtraBindings.Any();
}
