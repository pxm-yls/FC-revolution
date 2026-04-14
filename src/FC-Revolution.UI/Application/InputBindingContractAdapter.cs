using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal static class InputBindingContractAdapter
{
    private static readonly CoreInputBindingSchema FallbackSchema = CoreInputBindingSchema.CreateFallback();

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromEntries(IEnumerable<InputBindingEntry> entries)
        => BuildActionBindingsFromEntries(entries, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromEntries(
        IEnumerable<InputBindingEntry> entries,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            if (!inputBindingSchema.TryNormalizePortId(entry.PortId, out var portId))
            {
                continue;
            }

            if (!bindingsByPort.TryGetValue(portId, out var actionBindings))
            {
                actionBindings = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
                bindingsByPort[portId] = actionBindings;
            }

            actionBindings[entry.ActionId.Trim()] = entry.SelectedKey;
        }

        return bindingsByPort;
    }

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromPortMaps(
        IReadOnlyDictionary<string, Dictionary<string, Key>> portMaps)
        => BuildActionBindingsFromPortMaps(portMaps, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromPortMaps(
        IReadOnlyDictionary<string, Dictionary<string, Key>> portMaps,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(portMaps);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var portMap in portMaps)
        {
            if (!inputBindingSchema.TryNormalizePortId(portMap.Key, out var portId))
            {
                continue;
            }

            bindingsByPort[portId] = portMap.Value.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return bindingsByPort;
    }

    public static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> BuildActionBindingsByRomPath(
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides)
        => BuildActionBindingsByRomPath(romInputOverrides, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> BuildActionBindingsByRomPath(
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(romInputOverrides);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        return romInputOverrides.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, Dictionary<string, Key>>(
                BuildActionBindingsFromPortMaps(pair.Value, inputBindingSchema),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }
}
