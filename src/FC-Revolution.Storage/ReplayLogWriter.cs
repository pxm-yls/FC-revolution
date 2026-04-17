using System.Buffers.Binary;
using System.Text;

namespace FCRevolution.Storage;

/// <summary>Appends compact per-frame input records to a branch-local replay log.</summary>
public sealed class ReplayLogWriter : IDisposable
{
    private static ReadOnlySpan<byte> Magic => "FCRL"u8;
    private const byte ActionCatalogVersion = 3;

    private FileStream? _stream;
    private ReplayLogReader.ReplayLogHeader _header =
        new(ActionCatalogVersion, ReplayLogActionCatalog.CreateDefaultPortLayouts());

    public string? Path { get; private set; }

    public bool IsOpen => _stream != null;

    public void Open(
        string path,
        bool resetFile,
        IEnumerable<string>? portIds = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? actionIdsByPort = null)
    {
        Close();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var mode = resetFile ? FileMode.Create : FileMode.OpenOrCreate;
        _stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.Read);
        Path = path;

        if (_stream.Length == 0)
        {
            _header = new ReplayLogReader.ReplayLogHeader(
                ActionCatalogVersion,
                ReplayLogActionCatalog.CreatePortLayouts(portIds, actionIdsByPort));
            _stream.Write(Magic);
            _stream.WriteByte(ActionCatalogVersion);
            _stream.WriteByte((byte)_header.PortLayouts.Count);
            foreach (var portLayout in _header.PortLayouts)
            {
                var portBytes = Encoding.UTF8.GetBytes(portLayout.PortId);
                _stream.WriteByte((byte)portBytes.Length);
                _stream.Write(portBytes);
                _stream.WriteByte((byte)portLayout.ActionIds.Count);
                foreach (var actionId in portLayout.ActionIds)
                {
                    var actionBytes = Encoding.UTF8.GetBytes(actionId);
                    _stream.WriteByte((byte)actionBytes.Length);
                    _stream.Write(actionBytes);
                }
            }
            _stream.Flush();
            return;
        }

        _stream.Seek(0, SeekOrigin.Begin);
        _header = ReplayLogReader.ReadHeader(_stream);
        _stream.Seek(0, SeekOrigin.End);
    }

    public void Append(FrameInputRecord record)
    {
        if (_stream == null)
            return;

        var recordSize = 8 + _header.PortLayouts.Sum(static layout => layout.ByteLength);
        Span<byte> buffer = stackalloc byte[recordSize];
        BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], record.Frame);
        var offset = 8;
        foreach (var portLayout in _header.PortLayouts)
        {
            foreach (var actionId in record.GetPressedActions(portLayout.PortId))
            {
                if (!portLayout.TryGetBitIndex(actionId, out var bitIndex))
                    continue;

                var byteIndex = bitIndex / 8;
                var bitOffset = bitIndex % 8;
                buffer[offset + byteIndex] |= (byte)(1 << bitOffset);
            }

            offset += portLayout.ByteLength;
        }
        _stream.Write(buffer);
    }

    public void Flush() => _stream?.Flush();

    public void Close()
    {
        _stream?.Flush();
        _stream?.Dispose();
        _stream = null;
        Path = null;
        _header = new ReplayLogReader.ReplayLogHeader(
            ActionCatalogVersion,
            ReplayLogActionCatalog.CreateDefaultPortLayouts());
    }

    public void Dispose() => Close();
}
