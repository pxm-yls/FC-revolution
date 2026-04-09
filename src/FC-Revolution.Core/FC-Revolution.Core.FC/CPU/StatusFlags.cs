namespace FCRevolution.Core.CPU;

[Flags]
public enum StatusFlags : byte
{
    None     = 0x00,
    Carry    = 0x01,  // C
    Zero     = 0x02,  // Z
    IRQDisable = 0x04, // I
    Decimal  = 0x08,  // D (unused on NES)
    Break    = 0x10,  // B
    Unused   = 0x20,  // U (always 1)
    Overflow = 0x40,  // V
    Negative = 0x80,  // N
}
