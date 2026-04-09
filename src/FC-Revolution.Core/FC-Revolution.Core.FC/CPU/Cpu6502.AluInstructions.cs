namespace FCRevolution.Core.CPU;

public sealed partial class Cpu6502
{
    // Logic / ALU
    private void AND() { A &= Fetch(); SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void EOR() { A ^= Fetch(); SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void ORA() { A |= Fetch(); SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void BIT()
    {
        byte m = Fetch();
        SetFlag(StatusFlags.Zero,     (A & m) == 0);
        SetFlag(StatusFlags.Negative, (m & 0x80) != 0);
        SetFlag(StatusFlags.Overflow, (m & 0x40) != 0);
    }
    private void ADC()
    {
        ushort m = Fetch();
        ushort res = (ushort)(A + m + (GetFlag(StatusFlags.Carry) ? 1 : 0));
        SetFlag(StatusFlags.Carry,    res > 0xFF);
        SetFlag(StatusFlags.Zero,     (res & 0xFF) == 0);
        SetFlag(StatusFlags.Negative, (res & 0x80) != 0);
        SetFlag(StatusFlags.Overflow, ((~(A ^ m) & (A ^ res) & 0x80) != 0));
        A = (byte)(res & 0xFF);
    }
    private void SBC()
    {
        ushort m = (ushort)(Fetch() ^ 0xFF);
        ushort res = (ushort)(A + m + (GetFlag(StatusFlags.Carry) ? 1 : 0));
        SetFlag(StatusFlags.Carry,    (res & 0xFF00) != 0);
        SetFlag(StatusFlags.Zero,     (res & 0xFF) == 0);
        SetFlag(StatusFlags.Negative, (res & 0x80) != 0);
        SetFlag(StatusFlags.Overflow, ((res ^ A) & (res ^ m) & 0x80) != 0);
        A = (byte)(res & 0xFF);
    }

    // Compare
    private void CMP() { ushort m = Fetch(); ushort r = (ushort)(A - m); SetFlag(StatusFlags.Carry, A >= m); SetFlag(StatusFlags.Zero, (r & 0xFF) == 0); SetFlag(StatusFlags.Negative, (r & 0x80) != 0); }
    private void CPX() { ushort m = Fetch(); ushort r = (ushort)(X - m); SetFlag(StatusFlags.Carry, X >= m); SetFlag(StatusFlags.Zero, (r & 0xFF) == 0); SetFlag(StatusFlags.Negative, (r & 0x80) != 0); }
    private void CPY() { ushort m = Fetch(); ushort r = (ushort)(Y - m); SetFlag(StatusFlags.Carry, Y >= m); SetFlag(StatusFlags.Zero, (r & 0xFF) == 0); SetFlag(StatusFlags.Negative, (r & 0x80) != 0); }

    // Increment / Decrement
    private void INC() { byte m = (byte)(Fetch() + 1); Write(_absAddr, m); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, (m & 0x80) != 0); }
    private void INX() { X++; SetFlag(StatusFlags.Zero, X == 0); SetFlag(StatusFlags.Negative, (X & 0x80) != 0); }
    private void INY() { Y++; SetFlag(StatusFlags.Zero, Y == 0); SetFlag(StatusFlags.Negative, (Y & 0x80) != 0); }
    private void DEC() { byte m = (byte)(Fetch() - 1); Write(_absAddr, m); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, (m & 0x80) != 0); }
    private void DEX() { X--; SetFlag(StatusFlags.Zero, X == 0); SetFlag(StatusFlags.Negative, (X & 0x80) != 0); }
    private void DEY() { Y--; SetFlag(StatusFlags.Zero, Y == 0); SetFlag(StatusFlags.Negative, (Y & 0x80) != 0); }
}
