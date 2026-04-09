namespace FCRevolution.Core.APU.Channels;

public sealed class PulseChannel
{
    public bool Enabled { get; set; }

    private static readonly byte[][] DutyTable =
    {
        new byte[]{ 0,1,0,0,0,0,0,0 },
        new byte[]{ 0,1,1,0,0,0,0,0 },
        new byte[]{ 0,1,1,1,1,0,0,0 },
        new byte[]{ 1,0,0,1,1,1,1,1 },
    };

    private readonly bool _isChannel2;
    private int _duty, _dutyIndex;
    private bool _lengthHalt;
    private bool _constVol;
    private int _volume, _envelopeVol;
    private int _envelopeTimer, _envelopePeriod;
    private bool _envelopeStart;
    private int _timer, _timerPeriod;
    private int _lengthCounter;
    private bool _sweepEnabled;
    private int _sweepPeriod, _sweepTimer, _sweepShift;
    private bool _sweepNegate, _sweepReload;

    public PulseChannel(bool isChannel2) => _isChannel2 = isChannel2;

    public void WriteReg0(byte data)
    {
        _duty       = (data >> 6) & 3;
        _lengthHalt = (data & 0x20) != 0;
        _constVol   = (data & 0x10) != 0;
        _envelopePeriod = data & 0x0F;
        _volume     = data & 0x0F;
    }

    public void WriteReg1(byte data)
    {
        _sweepEnabled = (data & 0x80) != 0;
        _sweepPeriod  = ((data >> 4) & 7) + 1;
        _sweepNegate  = (data & 0x08) != 0;
        _sweepShift   = data & 0x07;
        _sweepReload  = true;
    }

    public void WriteReg2(byte data) => _timerPeriod = (_timerPeriod & 0x700) | data;

    public void WriteReg3(byte data)
    {
        _timerPeriod = (_timerPeriod & 0xFF) | ((data & 0x07) << 8);
        if (Enabled) _lengthCounter = LengthTable[(data >> 3) & 0x1F];
        _envelopeStart = true;
        _dutyIndex = 0;
    }

    public void ClockLength()
    {
        if (!_lengthHalt && _lengthCounter > 0) _lengthCounter--;
    }

    public void ClockEnvelope()
    {
        if (_envelopeStart)
        {
            _envelopeStart = false;
            _envelopeVol = 15;
            _envelopeTimer = _envelopePeriod;
        }
        else if (_envelopeTimer > 0) _envelopeTimer--;
        else
        {
            _envelopeTimer = _envelopePeriod;
            if (_envelopeVol > 0) _envelopeVol--;
            else if (_lengthHalt) _envelopeVol = 15;
        }
    }

    public void ClockSweep()
    {
        if (_sweepTimer == 0 && _sweepEnabled && _sweepShift > 0 && _timerPeriod >= 8)
        {
            int delta = _timerPeriod >> _sweepShift;
            if (_sweepNegate) delta = _isChannel2 ? -delta : -(delta + 1);
            int newPeriod = _timerPeriod + delta;
            if (newPeriod < 0x800) _timerPeriod = newPeriod;
        }
        if (_sweepTimer == 0 || _sweepReload)
        {
            _sweepTimer = _sweepPeriod;
            _sweepReload = false;
        }
        else _sweepTimer--;
    }

    public float GetSample()
    {
        if (!Enabled || _lengthCounter == 0 || _timerPeriod < 8) return 0f;
        int delta = _timerPeriod >> _sweepShift;
        if (!_sweepNegate && (_timerPeriod + delta) >= 0x800) return 0f;
        if (DutyTable[_duty][_dutyIndex] == 0) return 0f;
        int vol = _constVol ? _volume : _envelopeVol;
        return vol / 15.0f;
    }

    public void Clock()
    {
        if (_timer == 0) { _timer = _timerPeriod; _dutyIndex = (_dutyIndex + 1) & 7; }
        else _timer--;
    }

    public byte[] SerializeState()
    {
        var b = new byte[15];
        b[0]  = (byte)_duty;
        b[1]  = (byte)_dutyIndex;
        b[2]  = (byte)((_lengthHalt ? 1 : 0) | (_constVol ? 2 : 0) | (_envelopeStart ? 4 : 0)
                     | (_sweepEnabled ? 8 : 0) | (_sweepNegate ? 16 : 0) | (_sweepReload ? 32 : 0)
                     | (Enabled ? 64 : 0));
        b[3]  = (byte)_volume;
        b[4]  = (byte)_envelopeVol;
        b[5]  = (byte)_envelopeTimer;
        b[6]  = (byte)_envelopePeriod;
        b[7]  = (byte)_sweepPeriod;
        b[8]  = (byte)_sweepTimer;
        b[9]  = (byte)_sweepShift;
        b[10] = (byte)_lengthCounter;
        b[11] = (byte)(_timerPeriod & 0xFF); b[12] = (byte)((_timerPeriod >> 8) & 0xFF);
        b[13] = (byte)(_timer & 0xFF);       b[14] = (byte)((_timer >> 8) & 0xFF);
        return b;
    }

    public void DeserializeState(byte[] b)
    {
        if (b.Length < 15) return;
        _duty            = b[0];
        _dutyIndex       = b[1];
        byte f           = b[2];
        _lengthHalt      = (f & 1)  != 0; _constVol     = (f & 2)  != 0;
        _envelopeStart   = (f & 4)  != 0; _sweepEnabled = (f & 8)  != 0;
        _sweepNegate     = (f & 16) != 0; _sweepReload  = (f & 32) != 0;
        Enabled          = (f & 64) != 0;
        _volume          = b[3];
        _envelopeVol     = b[4];
        _envelopeTimer   = b[5];
        _envelopePeriod  = b[6];
        _sweepPeriod     = b[7];
        _sweepTimer      = b[8];
        _sweepShift      = b[9];
        _lengthCounter   = b[10];
        _timerPeriod     = b[11] | (b[12] << 8);
        _timer           = b[13] | (b[14] << 8);
    }

    private static readonly byte[] LengthTable =
    {
        10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,
        12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30
    };
}
