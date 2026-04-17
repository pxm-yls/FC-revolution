using FCRevolution.Core.Input;
using FCRevolution.Core.State;
using FCRevolution.Storage;

namespace FCRevolution.Core.Replay;

/// <summary>Rebuilds emulator state at arbitrary frame positions using a base snapshot and input log.</summary>
public sealed class ReplayPlayer
{
    private static readonly IReadOnlyDictionary<string, NesButton> LegacyButtonsByActionId =
        new Dictionary<string, NesButton>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = NesButton.A,
            ["b"] = NesButton.B,
            ["select"] = NesButton.Select,
            ["start"] = NesButton.Start,
            ["up"] = NesButton.Up,
            ["down"] = NesButton.Down,
            ["left"] = NesButton.Left,
            ["right"] = NesButton.Right,
        };

    private readonly string _romPath;
    private readonly byte[] _baseState;
    private readonly long _baseFrame;
    private readonly IReadOnlyList<FrameInputRecord> _records;
    private readonly NesConsole _console = new();

    public ReplayPlayer(string romPath, byte[] baseState, IReadOnlyList<FrameInputRecord> records)
    {
        _romPath = romPath;
        _baseState = baseState;
        _baseFrame = StateSnapshotSerializer.HasHeader(baseState) ? StateSnapshotSerializer.Deserialize(baseState).Frame : 0;
        _records = records.OrderBy(record => record.Frame).ToArray();

        _console.LoadRom(_romPath);
        _console.LoadState(_baseState);
        _console.Start();
    }

    public NesConsole Console => _console;

    public uint[] SeekToFrame(long targetFrame)
    {
        ResetToBaseState();

        var lastFrameBuffer = _console.Ppu.FrameBuffer;
        foreach (var record in _records)
        {
            if (record.Frame <= _baseFrame)
                continue;
            if (record.Frame > targetFrame)
                break;

            _console.SetControllerMask(0, BuildControllerMask(record, "p1"));
            _console.SetControllerMask(1, BuildControllerMask(record, "p2"));
            lastFrameBuffer = _console.RunFrame();
        }

        return lastFrameBuffer;
    }

    public List<uint[]> RenderFrameRange(long startFrame, long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "End frame must be greater than or equal to start frame.");

        ResetToBaseState();

        var frames = new List<uint[]>();
        foreach (var record in _records)
        {
            if (record.Frame <= _baseFrame)
                continue;
            if (record.Frame > endFrame)
                break;

            _console.SetControllerMask(0, BuildControllerMask(record, "p1"));
            _console.SetControllerMask(1, BuildControllerMask(record, "p2"));
            var frameBuffer = _console.RunFrame();
            if (record.Frame >= startFrame)
                frames.Add((uint[])frameBuffer.Clone());
        }

        return frames;
    }

    private void ResetToBaseState()
    {
        _console.LoadState(_baseState);
        _console.Start();
    }

    private static byte BuildControllerMask(FrameInputRecord record, string portId)
    {
        byte mask = 0;
        foreach (var actionId in record.GetPressedActions(portId))
        {
            if (LegacyButtonsByActionId.TryGetValue(actionId, out var button))
                mask |= (byte)button;
        }

        return mask;
    }
}
