using FCRevolution.Core.Mappers;

namespace FCRevolution.Core.PPU;

public sealed partial class Ppu2C02
{
    // ─────────────────────────────────────────────────────────────────────
    //  CPU register access ($2000–$2007, $4014)
    // ─────────────────────────────────────────────────────────────────────

    public byte ReadRegister(ushort addr)
    {
        byte data = 0;
        switch (addr & 0x07)
        {
            case 0x02: // PPUSTATUS
                data = (byte)((byte)Status & 0xE0 | (_ppuDataBuf & 0x1F));
                Status &= ~PpuStatus.VerticalBlank;
                _w = false;
                break;
            case 0x04: // OAMDATA
                data = Oam[_oamAddr];
                break;
            case 0x07: // PPUDATA
                data = _ppuDataBuf;
                _ppuDataBuf = ReadVram(_v);
                if (_v >= 0x3F00) data = _ppuDataBuf;
                _v += Control.HasFlag(PpuControl.IncrementMode) ? (ushort)32 : (ushort)1;
                break;
        }
        return data;
    }

    public void WriteRegister(ushort addr, byte data)
    {
        switch (addr & 0x07)
        {
            case 0x00: // PPUCTRL
                Control = (PpuControl)data;
                _t = (ushort)((_t & 0xF3FF) | ((data & 0x03) << 10));
                _patternBg = (data & 0x10) != 0;
                _patternSprite = (data & 0x08) != 0;
                _spriteSize16 = (data & 0x20) != 0;
                break;
            case 0x01: // PPUMASK
                Mask = (PpuMask)data;
                _showBg = (data & 0x08) != 0;
                _showBgLeft8 = (data & 0x02) != 0;
                _showSprites = (data & 0x10) != 0;
                _showSpriteLeft8 = (data & 0x04) != 0;
                _renderEnabled = _showBg || _showSprites;
                break;
            case 0x03: // OAMADDR
                _oamAddr = data;
                break;
            case 0x04: // OAMDATA
                Oam[_oamAddr++] = data;
                break;
            case 0x05: // PPUSCROLL
                if (!_w) { _t = (ushort)((_t & 0xFFE0) | (data >> 3)); _x = (byte)(data & 0x07); }
                else { _t = (ushort)((_t & 0x8FFF) | ((data & 0x07) << 12)); _t = (ushort)((_t & 0xFC1F) | ((data & 0xF8) << 2)); }
                _w = !_w;
                break;
            case 0x06: // PPUADDR
                if (!_w) { _t = (ushort)((_t & 0x00FF) | ((data & 0x3F) << 8)); }
                else { _t = (ushort)((_t & 0xFF00) | data); _v = _t; }
                _w = !_w;
                break;
            case 0x07: // PPUDATA
                WriteVram(_v, data);
                _v += Control.HasFlag(PpuControl.IncrementMode) ? (ushort)32 : (ushort)1;
                break;
        }
    }

    public void DmaWrite(byte page, byte[] cpuMem)
    {
        int baseAddr = page << 8;
        for (int i = 0; i < 256; i++)
            Oam[(_oamAddr + i) & 0xFF] = cpuMem[(baseAddr + i) & 0xFFFF];
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VRAM read/write
    // ─────────────────────────────────────────────────────────────────────

    private byte ReadVram(ushort addr)
    {
        addr &= 0x3FFF;
        if (addr < 0x3F00)
            PpuAddressObserver?.ObservePpuAddress(addr);
        if (addr < 0x2000) return _cart?.PpuRead(addr) ?? 0;
        if (addr < 0x3F00)
        {
            if (NametableProvider?.TryReadNametable(addr, out var data) == true)
                return data;
            return Vram[MirrorNametable(addr)];
        }
        return PaletteRam[MirrorPalette(addr)];
    }

    private void WriteVram(ushort addr, byte data)
    {
        addr &= 0x3FFF;
        if (addr < 0x3F00)
            PpuAddressObserver?.ObservePpuAddress(addr);
        if (addr < 0x2000) { _cart?.PpuWrite(addr, data); return; }
        if (addr < 0x3F00)
        {
            if (NametableProvider?.TryWriteNametable(addr, data) == true)
                return;
            Vram[MirrorNametable(addr)] = data;
            return;
        }
        PaletteRam[MirrorPalette(addr)] = data;
    }

    private int MirrorNametable(ushort addr)
    {
        int a = (addr - 0x2000) & 0x0FFF;
        var m = _cart?.Mirroring ?? MirroringMode.Horizontal;
        return m switch
        {
            MirroringMode.Horizontal => (a & 0x800) == 0 ? a & 0x03FF : 0x0400 + (a & 0x03FF),
            MirroringMode.Vertical => a & 0x07FF,
            MirroringMode.SingleLower => a & 0x03FF,
            MirroringMode.SingleUpper => 0x0400 + (a & 0x03FF),
            _ => a & 0x07FF,
        };
    }

    private static int MirrorPalette(ushort addr)
    {
        int a = (addr - 0x3F00) & 0x1F;
        if (a == 0x10 || a == 0x14 || a == 0x18 || a == 0x1C) a &= 0x0F;
        return a;
    }

    private static ushort RewindScrollXPrefetch(ushort v)
    {
        for (int i = 0; i < 2; i++)
        {
            if ((v & 0x001F) == 0)
            {
                v &= unchecked((ushort)~0x001F);
                v |= 0x001F;
                v ^= 0x0400;
            }
            else
            {
                v--;
            }
        }

        return v;
    }

    private static ushort AdvanceScrollX(ushort v)
    {
        if ((v & 0x001F) == 31)
        {
            v &= unchecked((ushort)~0x001F);
            v ^= 0x0400;
        }
        else
        {
            v++;
        }

        return v;
    }
}
