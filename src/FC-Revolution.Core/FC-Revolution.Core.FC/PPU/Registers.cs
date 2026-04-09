namespace FCRevolution.Core.PPU;

[Flags]
public enum PpuControl : byte
{
    None          = 0x00,
    NametableX    = 0x01,
    NametableY    = 0x02,
    IncrementMode = 0x04,  // 0=+1, 1=+32
    PatternSprite = 0x08,  // 0=$0000, 1=$1000
    PatternBg     = 0x10,  // 0=$0000, 1=$1000
    SpriteSize    = 0x20,  // 0=8x8, 1=8x16
    SlaveMode     = 0x40,
    NmiEnable     = 0x80,
}

[Flags]
public enum PpuMask : byte
{
    None            = 0x00,
    Grayscale       = 0x01,
    ShowBgLeft8     = 0x02,
    ShowSpriteLeft8 = 0x04,
    ShowBg         = 0x08,
    ShowSprites    = 0x10,
    EmphasizeR     = 0x20,
    EmphasizeG     = 0x40,
    EmphasizeB     = 0x80,
}

[Flags]
public enum PpuStatus : byte
{
    None           = 0x00,
    SpriteOverflow = 0x20,
    Sprite0Hit     = 0x40,
    VerticalBlank  = 0x80,
}
