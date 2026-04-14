using System.Buffers.Binary;
using System.Text;

namespace FCRevolution.Storage;

/// <summary>Appends compact per-frame input records to a branch-local replay log.</summary>
public sealed class ReplayLogWriter : IDisposable
{
    private static ReadOnlySpan<byte> Magic => "FCRL"u8;
    private const byte CurrentVersion = 2;

    private FileStream? _stream;
    private IReadOnlyList<string> _portIds = ["p1", "p2"];

    public string? Path { get; private set; }

    public bool IsOpen => _stream != null;

    public void Open(string path, bool resetFile, IEnumerable<string>? portIds = null)
    {
        Close();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var mode = resetFile ? FileMode.Create : FileMode.OpenOrCreate;
        _stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.Read);
        Path = path;

        if (_stream.Length == 0)
        {
            _portIds = NormalizePortIds(portIds);
            _stream.Write(Magic);
            _stream.WriteByte(CurrentVersion);
            _stream.WriteByte((byte)_portIds.Count);
            foreach (var portId in _portIds)
            {
                var portBytes = Encoding.UTF8.GetBytes(portId);
                _stream.WriteByte((byte)portBytes.Length);
                _stream.Write(portBytes);
            }
            _stream.Flush();
            return;
        }

        _stream.Seek(0, SeekOrigin.Begin);
        var header = ReplayLogReader.ReadHeader(_stream);
        _portIds = header.PortIds;
        _stream.Seek(0, SeekOrigin.End);
    }

    public void Append(FrameInputRecord record)
    {
        if (_stream == null)
            return;

        Span<byte> buffer = stackalloc byte[8 + _portIds.Count];
        BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], record.Frame);
        for (var index = 0; index < _portIds.Count; index++)
            buffer[8 + index] = record.GetButtonsMask(_portIds[index]);
        _stream.Write(buffer);
    }

    public void Flush() => _stream?.Flush();

    public void Close()
    {
        _stream?.Flush();
        _stream?.Dispose();
        _stream = null;
        Path = null;
        _portIds = ["p1", "p2"];
    }

    public void Dispose() => Close();

    private static IReadOnlyList<string> NormalizePortIds(IEnumerable<string>? portIds)
    {
        var normalized = (portIds ?? ["p1", "p2"])
            .Where(static portId => !string.IsNullOrWhiteSpace(portId))
            .Select(static portId => portId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? ["p1", "p2"] : normalized;
    }
}
