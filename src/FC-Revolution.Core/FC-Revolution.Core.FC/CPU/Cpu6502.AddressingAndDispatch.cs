namespace FCRevolution.Core.CPU;

public sealed partial class Cpu6502
{
    // ─────────────────────────────────────────────────────────────────────
    //  Addressing modes
    // ─────────────────────────────────────────────────────────────────────

    private void IMP() { _isAccumulator = true; }
    private void IMM() { _absAddr = PC++; }
    private void ZP0() { _absAddr = Read(PC++); }
    private void ZPX() { _absAddr = (ushort)((Read(PC++) + X) & 0xFF); }
    private void ZPY() { _absAddr = (ushort)((Read(PC++) + Y) & 0xFF); }
    private void ABS() { ushort lo = Read(PC++); ushort hi = Read(PC++); _absAddr = (ushort)((hi << 8) | lo); }
    private void ABX()
    {
        ushort lo = Read(PC++); ushort hi = Read(PC++);
        ushort base_ = (ushort)((hi << 8) | lo);
        _absAddr = (ushort)(base_ + X);
        if (PageCrossed(base_, _absAddr)) _extraCycles++;
    }
    private void ABY()
    {
        ushort lo = Read(PC++); ushort hi = Read(PC++);
        ushort base_ = (ushort)((hi << 8) | lo);
        _absAddr = (ushort)(base_ + Y);
        if (PageCrossed(base_, _absAddr)) _extraCycles++;
    }
    private void IND()
    {
        ushort lo = Read(PC++); ushort hi = Read(PC++);
        ushort ptr = (ushort)((hi << 8) | lo);
        // 6502 page-crossing bug
        if ((ptr & 0xFF) == 0xFF)
            _absAddr = (ushort)((Read((ushort)(ptr & 0xFF00)) << 8) | Read(ptr));
        else
            _absAddr = (ushort)((Read((ushort)(ptr + 1)) << 8) | Read(ptr));
    }
    private void IZX()
    {
        byte t = Read(PC++);
        ushort lo = Read((ushort)((t + X) & 0xFF));
        ushort hi = Read((ushort)((t + X + 1) & 0xFF));
        _absAddr = (ushort)((hi << 8) | lo);
    }
    private void IZY()
    {
        byte t = Read(PC++);
        ushort lo = Read((ushort)(t & 0xFF));
        ushort hi = Read((ushort)((t + 1) & 0xFF));
        ushort base_ = (ushort)((hi << 8) | lo);
        _absAddr = (ushort)(base_ + Y);
        if (PageCrossed(base_, _absAddr)) _extraCycles++;
    }
    private void REL() { _relAddr = Read(PC++); }

    // ─────────────────────────────────────────────────────────────────────
    //  Lookup table builder
    // ─────────────────────────────────────────────────────────────────────

    private void BuildLookupTables()
    {
        void R(byte op, Action exe, Action addr, int cy)
        {
            _instructionTable[op] = exe;
            _addrModeDispatch[op] = addr;
            _cycleTable[op] = cy;
        }
        for (int i = 0; i < 256; i++) { _instructionTable[i]=XXX; _addrModeDispatch[i]=IMP; _cycleTable[i]=2; }
        R(0x00,BRK,IMP,7); R(0x01,ORA,IZX,6); R(0x05,ORA,ZP0,3); R(0x06,ASL,ZP0,5);
        R(0x08,PHP,IMP,3); R(0x09,ORA,IMM,2); R(0x0A,ASL,IMP,2); R(0x0D,ORA,ABS,4);
        R(0x0E,ASL,ABS,6); R(0x10,BPL,REL,2); R(0x11,ORA,IZY,5); R(0x15,ORA,ZPX,4);
        R(0x16,ASL,ZPX,6); R(0x18,CLC,IMP,2); R(0x19,ORA,ABY,4); R(0x1D,ORA,ABX,4);
        R(0x1E,ASL,ABX,7); R(0x20,JSR,ABS,6); R(0x21,AND,IZX,6); R(0x24,BIT,ZP0,3);
        R(0x25,AND,ZP0,3); R(0x26,ROL,ZP0,5); R(0x28,PLP,IMP,4); R(0x29,AND,IMM,2);
        R(0x2A,ROL,IMP,2); R(0x2C,BIT,ABS,4); R(0x2D,AND,ABS,4); R(0x2E,ROL,ABS,6);
        R(0x30,BMI,REL,2); R(0x31,AND,IZY,5); R(0x35,AND,ZPX,4); R(0x36,ROL,ZPX,6);
        R(0x38,SEC,IMP,2); R(0x39,AND,ABY,4); R(0x3D,AND,ABX,4); R(0x3E,ROL,ABX,7);
        R(0x40,RTI,IMP,6); R(0x41,EOR,IZX,6); R(0x45,EOR,ZP0,3); R(0x46,LSR,ZP0,5);
        R(0x48,PHA,IMP,3); R(0x49,EOR,IMM,2); R(0x4A,LSR,IMP,2); R(0x4C,JMP,ABS,3);
        R(0x4D,EOR,ABS,4); R(0x4E,LSR,ABS,6); R(0x50,BVC,REL,2); R(0x51,EOR,IZY,5);
        R(0x55,EOR,ZPX,4); R(0x56,LSR,ZPX,6); R(0x58,CLI,IMP,2); R(0x59,EOR,ABY,4);
        R(0x5D,EOR,ABX,4); R(0x5E,LSR,ABX,7); R(0x60,RTS,IMP,6); R(0x61,ADC,IZX,6);
        R(0x65,ADC,ZP0,3); R(0x66,ROR,ZP0,5); R(0x68,PLA,IMP,4); R(0x69,ADC,IMM,2);
        R(0x6A,ROR,IMP,2); R(0x6C,JMP,IND,5); R(0x6D,ADC,ABS,4); R(0x6E,ROR,ABS,6);
        R(0x70,BVS,REL,2); R(0x71,ADC,IZY,5); R(0x75,ADC,ZPX,4); R(0x76,ROR,ZPX,6);
        R(0x78,SEI,IMP,2); R(0x79,ADC,ABY,4); R(0x7D,ADC,ABX,4); R(0x7E,ROR,ABX,7);
        R(0x81,STA,IZX,6); R(0x84,STY,ZP0,3); R(0x85,STA,ZP0,3); R(0x86,STX,ZP0,3);
        R(0x88,DEY,IMP,2); R(0x8A,TXA,IMP,2); R(0x8C,STY,ABS,4); R(0x8D,STA,ABS,4);
        R(0x8E,STX,ABS,4); R(0x90,BCC,REL,2); R(0x91,STA,IZY,6); R(0x94,STY,ZPX,4);
        R(0x95,STA,ZPX,4); R(0x96,STX,ZPY,4); R(0x98,TYA,IMP,2); R(0x99,STA,ABY,5);
        R(0x9A,TXS,IMP,2); R(0x9D,STA,ABX,5); R(0xA0,LDY,IMM,2); R(0xA1,LDA,IZX,6);
        R(0xA2,LDX,IMM,2); R(0xA4,LDY,ZP0,3); R(0xA5,LDA,ZP0,3); R(0xA6,LDX,ZP0,3);
        R(0xA8,TAY,IMP,2); R(0xA9,LDA,IMM,2); R(0xAA,TAX,IMP,2); R(0xAC,LDY,ABS,4);
        R(0xAD,LDA,ABS,4); R(0xAE,LDX,ABS,4); R(0xB0,BCS,REL,2); R(0xB1,LDA,IZY,5);
        R(0xB4,LDY,ZPX,4); R(0xB5,LDA,ZPX,4); R(0xB6,LDX,ZPY,4); R(0xB8,CLV,IMP,2);
        R(0xB9,LDA,ABY,4); R(0xBA,TSX,IMP,2); R(0xBC,LDY,ABX,4); R(0xBD,LDA,ABX,4);
        R(0xBE,LDX,ABY,4); R(0xC0,CPY,IMM,2); R(0xC1,CMP,IZX,6); R(0xC4,CPY,ZP0,3);
        R(0xC5,CMP,ZP0,3); R(0xC6,DEC,ZP0,5); R(0xC8,INY,IMP,2); R(0xC9,CMP,IMM,2);
        R(0xCA,DEX,IMP,2); R(0xCC,CPY,ABS,4); R(0xCD,CMP,ABS,4); R(0xCE,DEC,ABS,6);
        R(0xD0,BNE,REL,2); R(0xD1,CMP,IZY,5); R(0xD5,CMP,ZPX,4); R(0xD6,DEC,ZPX,6);
        R(0xD8,CLD,IMP,2); R(0xD9,CMP,ABY,4); R(0xDD,CMP,ABX,4); R(0xDE,DEC,ABX,7);
        R(0xE0,CPX,IMM,2); R(0xE1,SBC,IZX,6); R(0xE4,CPX,ZP0,3); R(0xE5,SBC,ZP0,3);
        R(0xE6,INC,ZP0,5); R(0xE8,INX,IMP,2); R(0xE9,SBC,IMM,2); R(0xEA,NOP,IMP,2);
        R(0xEC,CPX,ABS,4); R(0xED,SBC,ABS,4); R(0xEE,INC,ABS,6); R(0xF0,BEQ,REL,2);
        R(0xF1,SBC,IZY,5); R(0xF5,SBC,ZPX,4); R(0xF6,INC,ZPX,6); R(0xF8,SED,IMP,2);
        R(0xF9,SBC,ABY,4); R(0xFD,SBC,ABX,4); R(0xFE,INC,ABX,7);
    }

    private readonly Action[] _addrModeDispatch = new Action[256];
}
