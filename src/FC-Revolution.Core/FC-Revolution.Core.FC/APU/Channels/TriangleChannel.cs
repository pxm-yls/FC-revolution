namespace FCRevolution.Core.APU.Channels;

public sealed class TriangleChannel
{
    public bool Enabled { get; set; }

    private static readonly byte[] TriSeq = { 15,14,13,12,11,10,9,8,7,6,5,4,3,2,1,0,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15 };
    private int _seqIndex;
    private int _timer, _timerPeriod;
    private int _lengthCounter;
    private int _linearCounter, _linearPeriod;
    private bool _linearReload, _lengthHalt;

    public void WriteReg0(byte data) { _lengthHalt = (data & 0x80) != 0; _linearPeriod = data & 0x7F; }
    public void WriteReg2(byte data) => _timerPeriod = (_timerPeriod & 0x700) | data;
    public void WriteReg3(byte data)
    {
        _timerPeriod = (_timerPeriod & 0xFF) | ((data & 0x07) << 8);
        if (Enabled) _lengthCounter = LengthTable[(data >> 3) & 0x1F];
        _linearReload = true;
    }

    public void ClockLength() { if (!_lengthHalt && _lengthCounter > 0) _lengthCounter--; }
    public void ClockLinear()
    {
        if (_linearReload) _linearCounter = _linearPeriod;
        else if (_linearCounter > 0) _linearCounter--;
        if (!_lengthHalt) _linearReload = false;
    }

    public void Clock()
    {
        if (_timer == 0) { _timer = _timerPeriod; if (_lengthCounter > 0 && _linearCounter > 0) _seqIndex = (_seqIndex + 1) % 32; }
        else _timer--;
    }

    public float GetSample() => (!Enabled || _lengthCounter == 0 || _linearCounter == 0) ? 0f : TriSeq[_seqIndex] / 15.0f;

    public byte[] SerializeState()
    {
        var b = new byte[9];
        b[0]  = (byte)_seqIndex;
        b[1]  = (byte)((_lengthHalt ? 1 : 0) | (_linearReload ? 2 : 0) | (Enabled ? 4 : 0));
        b[2]  = (byte)_lengthCounter;
        b[3]  = (byte)_linearCounter;
        b[4]  = (byte)_linearPeriod;
        b[5]  = (byte)(_timerPeriod & 0xFF); b[6]  = (byte)((_timerPeriod >> 8) & 0xFF);
        b[7]  = (byte)(_timer & 0xFF);       b[8]  = (byte)((_timer >> 8) & 0xFF);
        return b;
    }

    public void DeserializeState(byte[] b)
    {
        if (b.Length < 9) return;
        _seqIndex     = b[0];
        byte f        = b[1];
        _lengthHalt   = (f & 1) != 0; _linearReload = (f & 2) != 0; Enabled = (f & 4) != 0;
        _lengthCounter  = b[2];
        _linearCounter  = b[3];
        _linearPeriod   = b[4];
        _timerPeriod    = b[5] | (b[6] << 8);
        _timer          = b[7] | (b[8] << 8);
    }

    private static readonly byte[] LengthTable = { 10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30 };
}
