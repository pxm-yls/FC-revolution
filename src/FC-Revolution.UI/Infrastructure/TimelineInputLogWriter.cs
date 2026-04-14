using System;
using System.Collections.Generic;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Infrastructure;

internal sealed class TimelineInputLogWriter : IDisposable
{
    private readonly ReplayLogWriter _writer = new();

    public bool IsOpen => _writer.IsOpen;

    public void Open(string path, bool resetFile, IEnumerable<string> portIds) => _writer.Open(path, resetFile, portIds);

    public void Close() => _writer.Close();

    public void Append(long frame, IReadOnlyDictionary<string, byte> masksByPort) =>
        _writer.Append(new FrameInputRecord(frame, masksByPort));

    public void Dispose() => _writer.Dispose();
}
