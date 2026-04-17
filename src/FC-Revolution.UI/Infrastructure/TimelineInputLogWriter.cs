using System;
using System.Collections.Generic;
using System.Linq;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Infrastructure;

internal sealed class TimelineInputLogWriter : IDisposable
{
    private readonly ReplayLogWriter _writer = new();

    public bool IsOpen => _writer.IsOpen;

    public void Open(string path, bool resetFile, CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var actionCatalog = inputBindingSchema.GetSupportedPorts()
            .ToDictionary(
                static port => port.PortId,
                port => (IReadOnlyList<string>)inputBindingSchema.GetSupportedActionIds(port.PortId),
                StringComparer.OrdinalIgnoreCase);
        _writer.Open(path, resetFile, actionCatalog.Keys, actionCatalog);
    }

    public void Close() => _writer.Close();

    public void Append(long frame, IReadOnlyDictionary<string, IReadOnlySet<string>> actionsByPort) =>
        _writer.Append(new FrameInputRecord(frame, actionsByPort));

    public void Dispose() => _writer.Dispose();
}
