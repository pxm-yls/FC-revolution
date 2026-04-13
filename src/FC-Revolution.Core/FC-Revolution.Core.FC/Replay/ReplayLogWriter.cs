using StorageFrameInputRecord = FCRevolution.Storage.FrameInputRecord;
using StorageReplayLogWriter = FCRevolution.Storage.ReplayLogWriter;

namespace FCRevolution.Core.Replay;

/// <summary>Appends compact per-frame input records to a branch-local replay log.</summary>
public sealed class ReplayLogWriter : IDisposable
{
    private readonly StorageReplayLogWriter _writer = new();

    public string? Path => _writer.Path;

    public bool IsOpen => _writer.IsOpen;

    public void Open(string path, bool resetFile) => _writer.Open(path, resetFile);

    public void Append(FrameInputRecord record) =>
        _writer.Append(new StorageFrameInputRecord(record.Frame, record.Player1ButtonsMask, record.Player2ButtonsMask));

    public void Flush() => _writer.Flush();

    public void Close() => _writer.Close();

    public void Dispose() => _writer.Dispose();
}
