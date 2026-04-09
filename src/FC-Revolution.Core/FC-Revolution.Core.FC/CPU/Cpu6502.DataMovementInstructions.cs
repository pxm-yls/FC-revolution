namespace FCRevolution.Core.CPU;

public sealed partial class Cpu6502
{
    // Load / Store
    private void LDA() { A = Fetch(); SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void LDX() { X = Fetch(); SetFlag(StatusFlags.Zero, X == 0); SetFlag(StatusFlags.Negative, (X & 0x80) != 0); }
    private void LDY() { Y = Fetch(); SetFlag(StatusFlags.Zero, Y == 0); SetFlag(StatusFlags.Negative, (Y & 0x80) != 0); }
    private void STA() { Write(_absAddr, A); }
    private void STX() { Write(_absAddr, X); }
    private void STY() { Write(_absAddr, Y); }

    // Transfer
    private void TAX() { X = A; SetFlag(StatusFlags.Zero, X == 0); SetFlag(StatusFlags.Negative, (X & 0x80) != 0); }
    private void TAY() { Y = A; SetFlag(StatusFlags.Zero, Y == 0); SetFlag(StatusFlags.Negative, (Y & 0x80) != 0); }
    private void TXA() { A = X; SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void TYA() { A = Y; SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void TSX() { X = S; SetFlag(StatusFlags.Zero, X == 0); SetFlag(StatusFlags.Negative, (X & 0x80) != 0); }
    private void TXS() { S = X; }

    // Stack
    private void PHA() { StackPush(A); }
    private void PHP() { StackPush((byte)(P | StatusFlags.Break | StatusFlags.Unused)); }
    private void PLA() { A = StackPop(); SetFlag(StatusFlags.Zero, A == 0); SetFlag(StatusFlags.Negative, (A & 0x80) != 0); }
    private void PLP() { P = (StatusFlags)(StackPop() & ~(byte)StatusFlags.Break) | StatusFlags.Unused; }
}
