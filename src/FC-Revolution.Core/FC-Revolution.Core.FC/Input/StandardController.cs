namespace FCRevolution.Core.Input;

public sealed class StandardController : IController
{
    private NesButton _state;
    private byte _shiftReg;
    private bool _strobe;

    public void SetButton(NesButton button, bool pressed)
    {
        if (pressed) _state |= button;
        else         _state &= ~button;
    }

    public void Write(byte data)
    {
        bool prev = _strobe;
        _strobe = (data & 0x01) != 0;
        // Falling edge of strobe: latch current button state
        if (prev && !_strobe)
            _shiftReg = (byte)_state;
    }

    public byte ReadState()
    {
        if (_strobe)
            return (byte)(((byte)_state & 0x01) | 0x40); // A button only while strobe high

        // Shift LSB-first: A(bit0) comes out first, matching NesButton enum order
        byte bit = (byte)(_shiftReg & 0x01);
        _shiftReg = (byte)((_shiftReg >> 1) | 0x80); // shift right, fill 1s
        return (byte)(bit | 0x40);
    }
}
