namespace FCRevolution.Core.CPU;

public sealed partial class Cpu6502
{
    // Shifts / rotates
    private void ASL()
    {
        byte m = _isAccumulator ? A : Fetch();
        SetFlag(StatusFlags.Carry, (m & 0x80) != 0);
        m = (byte)(m << 1); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, (m & 0x80) != 0);
        if (_isAccumulator) A = m; else Write(_absAddr, m);
    }
    private void LSR()
    {
        byte m = _isAccumulator ? A : Fetch();
        SetFlag(StatusFlags.Carry, (m & 0x01) != 0);
        m = (byte)(m >> 1); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, false);
        if (_isAccumulator) A = m; else Write(_absAddr, m);
    }
    private void ROL()
    {
        byte m = _isAccumulator ? A : Fetch();
        byte c = GetFlag(StatusFlags.Carry) ? (byte)1 : (byte)0;
        SetFlag(StatusFlags.Carry, (m & 0x80) != 0);
        m = (byte)((m << 1) | c); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, (m & 0x80) != 0);
        if (_isAccumulator) A = m; else Write(_absAddr, m);
    }
    private void ROR()
    {
        byte m = _isAccumulator ? A : Fetch();
        byte c = GetFlag(StatusFlags.Carry) ? (byte)0x80 : (byte)0;
        SetFlag(StatusFlags.Carry, (m & 0x01) != 0);
        m = (byte)((m >> 1) | c); SetFlag(StatusFlags.Zero, m == 0); SetFlag(StatusFlags.Negative, (m & 0x80) != 0);
        if (_isAccumulator) A = m; else Write(_absAddr, m);
    }

    // Jump / return / branch
    private void JMP() { PC = _absAddr; }
    private void JSR() { PC--; StackPush((byte)(PC >> 8)); StackPush((byte)(PC & 0xFF)); PC = _absAddr; }
    private void RTS() { PC = (ushort)(StackPop() | (StackPop() << 8)); PC++; }
    private void RTI()
    {
        P = (StatusFlags)StackPop();
        P &= ~StatusFlags.Break;
        P |= StatusFlags.Unused;
        PC = (ushort)(StackPop() | (StackPop() << 8));
    }

    private void BranchIf(bool cond)
    {
        if (!cond) return;
        _extraCycles++;
        ushort target = (ushort)(PC + (sbyte)_relAddr);
        if (PageCrossed(PC, target)) _extraCycles++;
        PC = target;
    }
    private void BCC() { BranchIf(!GetFlag(StatusFlags.Carry)); }
    private void BCS() { BranchIf( GetFlag(StatusFlags.Carry)); }
    private void BEQ() { BranchIf( GetFlag(StatusFlags.Zero)); }
    private void BNE() { BranchIf(!GetFlag(StatusFlags.Zero)); }
    private void BMI() { BranchIf( GetFlag(StatusFlags.Negative)); }
    private void BPL() { BranchIf(!GetFlag(StatusFlags.Negative)); }
    private void BVC() { BranchIf(!GetFlag(StatusFlags.Overflow)); }
    private void BVS() { BranchIf( GetFlag(StatusFlags.Overflow)); }

    // Flags
    private void CLC() { SetFlag(StatusFlags.Carry,      false); }
    private void CLD() { SetFlag(StatusFlags.Decimal,    false); }
    private void CLI() { SetFlag(StatusFlags.IRQDisable, false); }
    private void CLV() { SetFlag(StatusFlags.Overflow,   false); }
    private void SEC() { SetFlag(StatusFlags.Carry,      true); }
    private void SED() { SetFlag(StatusFlags.Decimal,    true); }
    private void SEI() { SetFlag(StatusFlags.IRQDisable, true); }

    // Misc
    private void NOP() { }
    private void BRK()
    {
        PC++;
        SetFlag(StatusFlags.IRQDisable, true);
        StackPush((byte)(PC >> 8));
        StackPush((byte)(PC & 0xFF));
        SetFlag(StatusFlags.Break, true);
        StackPush((byte)P);
        SetFlag(StatusFlags.Break, false);
        PC = (ushort)(Read(0xFFFE) | (Read(0xFFFF) << 8));
    }

    // Unofficial / illegal – minimal stubs (common ones used by games)
    private void XXX() { }   // Unknown/illegal – treat as NOP
}
