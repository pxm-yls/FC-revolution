using FCRevolution.Core.Input;
using FCRevolution.Core.State;

namespace FCRevolution.Core.Replay;

/// <summary>Rebuilds emulator state at arbitrary frame positions using a base snapshot and input log.</summary>
public sealed class ReplayPlayer
{
    private static readonly NesButton[] Buttons =
    [
        NesButton.A,
        NesButton.B,
        NesButton.Select,
        NesButton.Start,
        NesButton.Up,
        NesButton.Down,
        NesButton.Left,
        NesButton.Right,
    ];

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

            _console.SetControllerMask(0, record.Player1ButtonsMask);
            _console.SetControllerMask(1, record.Player2ButtonsMask);
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

            _console.SetControllerMask(0, record.Player1ButtonsMask);
            _console.SetControllerMask(1, record.Player2ButtonsMask);
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
}
