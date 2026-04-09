namespace FCRevolution.Core.PPU;

public sealed partial class Ppu2C02
{
    // ─────────────────────────────────────────────────────────────────────
    //  Sprite evaluation
    // ─────────────────────────────────────────────────────────────────────

    private void EvaluateSprites()
    {
        _spriteCount = 0;
        _sprite0InOam2 = false;
        _sprite0Rendering = false;

        int spriteH = _spriteSize16 ? 16 : 8;

        for (int i = 0; i < 64; i++)
        {
            int sprY = Oam[i * 4];
            int diff = Scanline - sprY;
            if (diff >= 0 && diff < spriteH)
            {
                if (i == 0) _sprite0InOam2 = true;
                if (_spriteCount < 8)
                {
                    _oam2[_spriteCount * 4]     = Oam[i * 4];
                    _oam2[_spriteCount * 4 + 1] = Oam[i * 4 + 1];
                    _oam2[_spriteCount * 4 + 2] = Oam[i * 4 + 2];
                    _oam2[_spriteCount * 4 + 3] = Oam[i * 4 + 3];
                }
                _spriteCount++;
            }
        }

        if (_spriteCount > 8) { Status |= PpuStatus.SpriteOverflow; _spriteCount = 8; }

        // Load sprite shift registers
        for (int i = 0; i < _spriteCount; i++)
        {
            int sprY  = _oam2[i * 4];
            byte tile = _oam2[i * 4 + 1];
            byte attr = _oam2[i * 4 + 2];
            byte x    = _oam2[i * 4 + 3];
            bool flipV = (attr & 0x80) != 0;
            bool flipH = (attr & 0x40) != 0;

            _spriteAttr[i] = attr;
            _spriteX[i]    = x;
            if (i == 0) _sprite0Rendering = _sprite0InOam2;

            int row = Scanline - sprY;
            if (flipV) row = spriteH - 1 - row;

            ushort tileAddr;
            if (!_spriteSize16)
            {
                tileAddr = (ushort)((_patternSprite ? 0x1000 : 0) + tile * 16 + row);
            }
            else
            {
                int bank = (tile & 0x01) * 0x1000;
                int t = tile & 0xFE;
                if (row >= 8) { t++; row -= 8; }
                tileAddr = (ushort)(bank + t * 16 + row);
            }

            byte lo = ReadVram(tileAddr);
            byte hi = ReadVram((ushort)(tileAddr + 8));

            if (flipH)
            {
                static byte Flip(byte b) { b = (byte)((b & 0xF0) >> 4 | (b & 0x0F) << 4); b = (byte)((b & 0xCC) >> 2 | (b & 0x33) << 2); b = (byte)((b & 0xAA) >> 1 | (b & 0x55) << 1); return b; }
                lo = Flip(lo); hi = Flip(hi);
            }

            _spriteShiftLo[i] = lo;
            _spriteShiftHi[i] = hi;
        }
    }
}
