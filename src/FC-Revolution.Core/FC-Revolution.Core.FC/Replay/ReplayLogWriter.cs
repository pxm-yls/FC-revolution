using System.Buffers.Binary;

namespace FCRevolution.Core.Replay;

/// <summary>Appends compact per-frame input records to a branch-local replay log.</summary>
public sealed class ReplayLogWriter : IDisposable
{
    private static ReadOnlySpan<byte> Magic => "FCRL"u8;
    private const byte CurrentVersion = 1;

    private FileStream? _stream;

    public string? Path { get; private set; }

    public bool IsOpen => _stream != null;

    public void Open(string path, bool resetFile)
    {
        Close();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var mode = resetFile || !File.Exists(path) ? FileMode.Create : FileMode.Append;
        _stream = new FileStream(path, mode, FileAccess.Write, FileShare.Read);
        Path = path;

        if (_stream.Length == 0)
        {
            _stream.Write(Magic);
            _stream.WriteByte(CurrentVersion);
            _stream.Flush();
        }
    }

    public void Append(FrameInputRecord record)
    {
        if (_stream == null)
            return;

        Span<byte> buffer = stackalloc byte[10];
        BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], record.Frame);
        buffer[8] = record.Player1ButtonsMask;
        buffer[9] = record.Player2ButtonsMask;
        _stream.Write(buffer);
    }

    public void Flush() => _stream?.Flush();

    public void Close()
    {
        _stream?.Flush();
        _stream?.Dispose();
        _stream = null;
        Path = null;
    }

    public void Dispose() => Close();
}
