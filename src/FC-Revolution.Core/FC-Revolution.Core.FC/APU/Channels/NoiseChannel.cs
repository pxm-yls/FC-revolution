namespace FCRevolution.Core.APU.Channels;

public sealed class NoiseChannel
{
    public bool Enabled { get; set; }

    private static readonly ushort[] NoisePeriod = { 4,8,16,32,64,96,128,160,202,254,380,508,762,1016,2034,4068 };
    private ushort _shiftReg = 1;
    private int _timer, _timerPeriod;
    private bool _mode;
    private int _lengthCounter;
    private bool _lengthHalt, _constVol;
    private int _volume, _envelopeVol, _envelopeTimer, _envelopePeriod;
    private bool _envelopeStart;

    public void WriteReg0(byte data) { _lengthHalt = (data & 0x20) != 0; _constVol = (data & 0x10) != 0; _envelopePeriod = data & 0x0F; _volume = data & 0x0F; }
    public void WriteReg2(byte data) { _mode = (data & 0x80) != 0; _timerPeriod = NoisePeriod[data & 0x0F]; }
    public void WriteReg3(byte data) { if (Enabled) _lengthCounter = LengthTable[(data >> 3) & 0x1F]; _envelopeStart = true; }

    public void ClockLength() { if (!_lengthHalt && _lengthCounter > 0) _lengthCounter--; }
    public void ClockEnvelope()
    {
        if (_envelopeStart) { _envelopeStart = false; _envelopeVol = 15; _envelopeTimer = _envelopePeriod; }
        else if (_envelopeTimer > 0) _envelopeTimer--;
        else { _envelopeTimer = _envelopePeriod; if (_envelopeVol > 0) _envelopeVol--; else if (_lengthHalt) _envelopeVol = 15; }
    }

    public void Clock()
    {
        if (_timer == 0)
        {
            _timer = _timerPeriod;
            int feedback = (_shiftReg & 1) ^ ((_mode ? (_shiftReg >> 6) : (_shiftReg >> 1)) & 1);
            _shiftReg >>= 1;
            _shiftReg |= (ushort)(feedback << 14);
        }
        else _timer--;
    }

    public float GetSample()
    {
        if (!Enabled || _lengthCounter == 0 || (_shiftReg & 1) != 0) return 0f;
        return (_constVol ? _volume : _envelopeVol) / 15.0f;
    }

    public byte[] SerializeState()
    {
        var b = new byte[12];
        b[0]  = (byte)(_shiftReg & 0xFF); b[1]  = (byte)((_shiftReg >> 8) & 0xFF);
        b[2]  = (byte)((_mode ? 1 : 0) | (_lengthHalt ? 2 : 0) | (_constVol ? 4 : 0)
                     | (_envelopeStart ? 8 : 0) | (Enabled ? 16 : 0));
        b[3]  = (byte)_volume;
        b[4]  = (byte)_envelopeVol;
        b[5]  = (byte)_envelopeTimer;
        b[6]  = (byte)_envelopePeriod;
        b[7]  = (byte)_lengthCounter;
        b[8]  = (byte)(_timerPeriod & 0xFF); b[9]  = (byte)((_timerPeriod >> 8) & 0xFF);
        b[10] = (byte)(_timer & 0xFF);       b[11] = (byte)((_timer >> 8) & 0xFF);
        return b;
    }

    public void DeserializeState(byte[] b)
    {
        if (b.Length < 12) return;
        _shiftReg       = (ushort)(b[0] | (b[1] << 8));
        byte f          = b[2];
        _mode           = (f & 1)  != 0; _lengthHalt   = (f & 2)  != 0;
        _constVol       = (f & 4)  != 0; _envelopeStart= (f & 8)  != 0;
        Enabled         = (f & 16) != 0;
        _volume         = b[3];
        _envelopeVol    = b[4];
        _envelopeTimer  = b[5];
        _envelopePeriod = b[6];
        _lengthCounter  = b[7];
        _timerPeriod    = b[8]  | (b[9]  << 8);
        _timer          = b[10] | (b[11] << 8);
    }

    private static readonly byte[] LengthTable = { 10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30 };
}
