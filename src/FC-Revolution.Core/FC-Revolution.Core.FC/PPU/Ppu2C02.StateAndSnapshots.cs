using FCRevolution.Core.Mappers;

namespace FCRevolution.Core.PPU;

public sealed partial class Ppu2C02
{
    public PpuRenderStateSnapshot CaptureRenderStateSnapshot()
    {
        var mirroringMode = _cart?.Mirroring ?? MirroringMode.Horizontal;

        return new PpuRenderStateSnapshot
        {
            NametableBytes = CaptureNametableBytes(mirroringMode),
            PatternTableBytes = CapturePatternTableBytes(),
            PaletteColors = CapturePaletteColors(),
            OamBytes = (byte[])Oam.Clone(),
            MirroringMode = mirroringMode,
            FineScrollX = _x,
            FineScrollY = (_v >> 12) & 0x07,
            CoarseScrollX = _v & 0x1F,
            CoarseScrollY = (_v >> 5) & 0x1F,
            NametableSelect = (_v >> 10) & 0x03,
            UseBackgroundPatternTableHighBank = _patternBg,
            UseSpritePatternTableHighBank = _patternSprite,
            Use8x16Sprites = _spriteSize16,
            ShowBackground = _showBg,
            ShowSprites = _showSprites,
            ShowBackgroundLeft8 = _showBgLeft8,
            ShowSpritesLeft8 = _showSpriteLeft8,
            HasCapturedBackgroundScanlineStates = _hasCapturedBackgroundScanlineStates,
            BackgroundScanlineStates = (PpuBackgroundScanlineState[])_backgroundScanlineStates.Clone()
        };
    }

    private byte[] CaptureNametableBytes(MirroringMode mirroringMode)
    {
        if (mirroringMode == MirroringMode.FourScreen)
        {
            var logicalBytes = new byte[4096];
            for (int page = 0; page < 4; page++)
                CopyLogicalNametablePage(page, logicalBytes, page * 1024);
            return logicalBytes;
        }

        var physicalBytes = new byte[2048];
        CopyLogicalNametablePage(GetRepresentativeLogicalNametablePage(mirroringMode, physicalPageIndex: 0), physicalBytes, 0);
        CopyLogicalNametablePage(GetRepresentativeLogicalNametablePage(mirroringMode, physicalPageIndex: 1), physicalBytes, 1024);
        return physicalBytes;
    }

    private void CopyLogicalNametablePage(int logicalPageIndex, byte[] destination, int destinationOffset)
    {
        ushort startAddress = (ushort)(0x2000 + (logicalPageIndex * 0x0400));
        for (int i = 0; i < 1024; i++)
            destination[destinationOffset + i] = ReadVram((ushort)(startAddress + i));
    }

    private static int GetRepresentativeLogicalNametablePage(MirroringMode mirroringMode, int physicalPageIndex) =>
        mirroringMode switch
        {
            MirroringMode.Horizontal => physicalPageIndex == 0 ? 0 : 2,
            MirroringMode.Vertical => physicalPageIndex,
            MirroringMode.SingleLower => 0,
            MirroringMode.SingleUpper => 1,
            _ => physicalPageIndex
        };

    private byte[] CapturePatternTableBytes()
    {
        var bytes = new byte[0x2000];
        if (_cart is null)
            return bytes;

        for (ushort address = 0; address < bytes.Length; address++)
            bytes[address] = _cart.PpuRead(address);

        return bytes;
    }

    private uint[] CapturePaletteColors()
    {
        var colors = new uint[PaletteRam.Length];
        for (int i = 0; i < PaletteRam.Length; i++)
            colors[i] = NesPalette[PaletteRam[i] & 0x3F];
        return colors;
    }

    public byte[] SerializeState()
    {
        var buffer = new byte[2363];
        var offset = 0;

        Vram.CopyTo(buffer, offset);
        offset += Vram.Length;
        PaletteRam.CopyTo(buffer, offset);
        offset += PaletteRam.Length;
        Oam.CopyTo(buffer, offset);
        offset += Oam.Length;

        buffer[offset++] = (byte)Control;
        buffer[offset++] = (byte)Mask;
        buffer[offset++] = (byte)Status;
        buffer[offset++] = (byte)(_v & 0xFF);
        buffer[offset++] = (byte)(_v >> 8);
        buffer[offset++] = (byte)(_t & 0xFF);
        buffer[offset++] = (byte)(_t >> 8);
        buffer[offset++] = _x;
        buffer[offset++] = _w ? (byte)1 : (byte)0;
        buffer[offset++] = _ppuDataBuf;
        buffer[offset++] = _oamAddr;

        WriteInt(buffer, ref offset, Scanline);
        WriteInt(buffer, ref offset, Cycle);
        WriteLong(buffer, ref offset, Frame);
        return buffer;
    }

    public void DeserializeState(byte[] state)
    {
        Array.Copy(state, 0, Vram, 0, 2048);
        Array.Copy(state, 2048, PaletteRam, 0, 32);
        Array.Copy(state, 2080, Oam, 0, 256);
        int o = 2336;
        Control = (PpuControl)state[o++];
        Mask = (PpuMask)state[o++];
        Status = (PpuStatus)state[o++];
        _v = (ushort)(state[o] | (state[o + 1] << 8));
        o += 2;
        _t = (ushort)(state[o] | (state[o + 1] << 8));
        o += 2;
        _x = state[o++];
        _w = state[o++] != 0;
        _ppuDataBuf = state[o++];
        _oamAddr = state[o++];
        if (state.Length > o + 15)
        {
            int ReadInt() { int v = state[o] | (state[o + 1] << 8) | (state[o + 2] << 16) | (state[o + 3] << 24); o += 4; return v; }
            long ReadLong() { long v = 0; for (int i = 0; i < 8; i++) { v |= (long)state[o + i] << (i * 8); } o += 8; return v; }
            Scanline = ReadInt();
            Cycle = ReadInt();
            Frame = ReadLong();
        }
    }

    private static void WriteInt(byte[] buffer, ref int offset, int value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
        buffer[offset++] = (byte)((value >> 16) & 0xFF);
        buffer[offset++] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteLong(byte[] buffer, ref int offset, long value)
    {
        for (var i = 0; i < 8; i++)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }
}
