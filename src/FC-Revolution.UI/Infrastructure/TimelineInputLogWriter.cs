using System;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Infrastructure;

internal sealed class TimelineInputLogWriter : IDisposable
{
    private readonly ReplayLogWriter _writer = new();

    public bool IsOpen => _writer.IsOpen;

    public void Open(string path, bool resetFile) => _writer.Open(path, resetFile);

    public void Close() => _writer.Close();

    public void Append(long frame, byte player1Mask, byte player2Mask) =>
        _writer.Append(new FrameInputRecord(frame, player1Mask, player2Mask));

    public void Dispose() => _writer.Dispose();
}
