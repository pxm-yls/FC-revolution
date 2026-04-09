namespace FCRevolution.Core.PPU;

public sealed partial class Ppu2C02
{
    // ─────────────────────────────────────────────────────────────────────
    //  Background helpers
    // ─────────────────────────────────────────────────────────────────────

    private void FetchBackground()
    {
        switch (Cycle & 0x07)
        {
            case 1:
                _ntByte = ReadVram((ushort)(0x2000 | (_v & 0x0FFF)));
                break;
            case 3:
                _atByte = ReadVram((ushort)(0x23C0 | (_v & 0x0C00) | ((_v >> 4) & 0x38) | ((_v >> 2) & 0x07)));
                if ((_v & 0x40) != 0) _atByte >>= 4;
                if ((_v & 0x02) != 0) _atByte >>= 2;
                _atByte &= 0x03;
                break;
            case 5:
                _bgLoTile = ReadVram((ushort)((_patternBg ? 0x1000 : 0) + _ntByte * 16 + ((_v >> 12) & 7)));
                break;
            case 7:
                _bgHiTile = ReadVram((ushort)((_patternBg ? 0x1000 : 0) + _ntByte * 16 + ((_v >> 12) & 7) + 8));
                break;
            case 0:
                LoadShifters();
                IncrementScrollX();
                break;
        }
    }

    private void LoadShifters()
    {
        _bgShiftLo = (ushort)((_bgShiftLo & 0xFF00) | _bgLoTile);
        _bgShiftHi = (ushort)((_bgShiftHi & 0xFF00) | _bgHiTile);
        _atShiftLo = (ushort)((_atShiftLo & 0xFF00) | ((_atByte & 0x01) != 0 ? 0xFF : 0));
        _atShiftHi = (ushort)((_atShiftHi & 0xFF00) | ((_atByte & 0x02) != 0 ? 0xFF : 0));
    }

    private void ShiftBackground()
    {
        if (_showBg)
        {
            _bgShiftLo <<= 1; _bgShiftHi <<= 1;
            _atShiftLo <<= 1; _atShiftHi <<= 1;
        }
        if (_showSprites && Cycle >= 1 && Cycle <= 256)
        {
            for (int i = 0; i < _spriteCount; i++)
            {
                if (_spriteX[i] > 0) _spriteX[i]--;
                else { _spriteShiftLo[i] <<= 1; _spriteShiftHi[i] <<= 1; }
            }
        }
    }

    private void IncrementScrollX()
    {
        if (!_renderEnabled) return;
        if ((_v & 0x001F) == 31) { _v &= unchecked((ushort)~0x001F); _v ^= 0x0400; }
        else _v++;
    }

    private void IncrementScrollY()
    {
        if (!_renderEnabled) return;
        if ((_v & 0x7000) != 0x7000) { _v += 0x1000; }
        else
        {
            _v &= unchecked((ushort)~0x7000);
            int y = (_v & 0x03E0) >> 5;
            if (y == 29) { y = 0; _v ^= 0x0800; }
            else if (y == 31) y = 0;
            else y++;
            _v = (ushort)((_v & ~0x03E0) | (y << 5));
        }
    }

    private void CopyScrollX()
    {
        if (!_renderEnabled) return;
        _v = (ushort)((_v & ~0x041F) | (_t & 0x041F));
    }

    private void CopyScrollY()
    {
        _v = (ushort)((_v & ~0x7BE0) | (_t & 0x7BE0));
    }
}
