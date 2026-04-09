namespace FCRevolution.Core.APU.Channels;

public sealed class DmcChannel
{
    public bool Enabled { get; set; }
    public bool IrqActive { get; private set; }

    private static readonly ushort[] RateTable = { 428,380,340,320,286,254,226,214,190,160,142,128,106,84,72,54 };

    private int _timer, _timerPeriod;
    private byte _outputLevel;
    private ushort _sampleAddr = 0xC000, _currentAddr;
    private int _sampleLength, _bytesRemaining;
    private byte _sampleBuffer;
    private bool _bufferFull;
    private byte _shiftReg;
    private int _bitsRemaining = 8;
    private bool _loop, _irqEnable;

    public Func<ushort, byte>? DmaRead { get; set; }

    public void WriteReg0(byte data) { _irqEnable = (data & 0x80) != 0; _loop = (data & 0x40) != 0; _timerPeriod = RateTable[data & 0x0F]; }
    public void WriteReg1(byte data) { _outputLevel = (byte)(data & 0x7F); }
    public void WriteReg2(byte data) { _sampleAddr = (ushort)(0xC000 + data * 64); }
    public void WriteReg3(byte data) { _sampleLength = data * 16 + 1; }

    public void Clock()
    {
        if (!Enabled) return;
        if (_timer == 0)
        {
            _timer = _timerPeriod;
            if (_bitsRemaining > 0)
            {
                if ((_shiftReg & 1) != 0) { if (_outputLevel <= 125) _outputLevel += 2; }
                else { if (_outputLevel >= 2) _outputLevel -= 2; }
                _shiftReg >>= 1;
                _bitsRemaining--;
            }
            if (_bitsRemaining == 0)
            {
                _bitsRemaining = 8;
                if (_bufferFull) { _shiftReg = _sampleBuffer; _bufferFull = false; }
            }
            if (!_bufferFull && _bytesRemaining > 0 && DmaRead != null)
            {
                _sampleBuffer = DmaRead(_currentAddr);
                _bufferFull = true;
                _currentAddr = (ushort)((_currentAddr + 1) | 0x8000);
                _bytesRemaining--;
                if (_bytesRemaining == 0)
                {
                    if (_loop) { _currentAddr = _sampleAddr; _bytesRemaining = _sampleLength; }
                    else if (_irqEnable) IrqActive = true;
                }
            }
        }
        else _timer--;
    }

    public float GetSample() => _outputLevel / 127.0f;
    public void ClearIrq() => IrqActive = false;

    public byte[] SerializeState()
    {
        var b = new byte[16];
        b[0]  = (byte)((_loop ? 1 : 0) | (_irqEnable ? 2 : 0) | (_bufferFull ? 4 : 0)
                     | (IrqActive ? 8 : 0) | (Enabled ? 16 : 0));
        b[1]  = _outputLevel;
        b[2]  = _sampleBuffer;
        b[3]  = (byte)_bitsRemaining;
        b[4]  = _shiftReg;
        b[5]  = (byte)(_timerPeriod & 0xFF); b[6]  = (byte)((_timerPeriod >> 8) & 0xFF);
        b[7]  = (byte)(_timer & 0xFF);       b[8]  = (byte)((_timer >> 8) & 0xFF);
        b[9]  = (byte)(_sampleAddr & 0xFF);  b[10] = (byte)((_sampleAddr >> 8) & 0xFF);
        b[11] = (byte)(_currentAddr & 0xFF); b[12] = (byte)((_currentAddr >> 8) & 0xFF);
        b[13] = (byte)(_sampleLength & 0xFF); b[14] = (byte)((_sampleLength >> 8) & 0xFF);
        b[15] = (byte)(_bytesRemaining & 0xFF);
        return b;
    }

    public void DeserializeState(byte[] b)
    {
        if (b.Length < 16) return;
        byte f     = b[0];
        _loop           = (f & 1)  != 0; _irqEnable  = (f & 2)  != 0;
        _bufferFull     = (f & 4)  != 0; IrqActive   = (f & 8)  != 0;
        Enabled         = (f & 16) != 0;
        _outputLevel    = b[1];
        _sampleBuffer   = b[2];
        _bitsRemaining  = b[3];
        _shiftReg       = b[4];
        _timerPeriod    = b[5]  | (b[6]  << 8);
        _timer          = b[7]  | (b[8]  << 8);
        _sampleAddr     = (ushort)(b[9]  | (b[10] << 8));
        _currentAddr    = (ushort)(b[11] | (b[12] << 8));
        _sampleLength   = b[13] | (b[14] << 8);
        _bytesRemaining = b[15];
    }
}
