using FCRevolution.Core.Mappers;

namespace FCRevolution.Core.PPU;

public sealed partial class Ppu2C02 : IEmulationComponent, IStateSerializable
{
    // ── Memory ────────────────────────────────────────────────────────────
    public readonly byte[] Vram       = new byte[2048];
    public readonly byte[] PaletteRam = new byte[32];
    public readonly byte[] Oam        = new byte[256];
    private readonly byte[] _oam2     = new byte[32];

    // ── Registers ─────────────────────────────────────────────────────────
    public PpuControl Control { get; private set; }
    public PpuMask    Mask    { get; private set; }
    public PpuStatus  Status  { get; private set; }

    // ── Loopy scroll registers ─────────────────────────────────────────────
    private ushort _v, _t;
    private byte   _x;
    private bool   _w;

    // ── Timing ────────────────────────────────────────────────────────────
    public int  Scanline { get; set; } = -1;
    public int  Cycle    { get; set; }
    public long Frame    { get; set; }
    public bool FrameComplete { get; set; }

    // ── Output frame buffer ───────────────────────────────────────────────
    public readonly uint[] FrameBuffer = new uint[NesConstants.FrameBufferSize];

    // ── Internal latches ──────────────────────────────────────────────────
    private byte _ntByte, _atByte, _bgLoTile, _bgHiTile;
    private ushort _bgShiftLo, _bgShiftHi, _atShiftLo, _atShiftHi;
    private byte _oamAddr;
    private byte _ppuDataBuf;
    public bool NmiPending { get; set; }

    // ── Sprite evaluation ─────────────────────────────────────────────────
    private int _spriteCount;
    private readonly byte[] _spriteShiftLo = new byte[8];
    private readonly byte[] _spriteShiftHi = new byte[8];
    private readonly byte[] _spriteAttr    = new byte[8];
    private readonly byte[] _spriteX       = new byte[8];
    private bool _sprite0InOam2;
    private bool _sprite0Rendering;

    // ── Cached register flags (updated on $2000/$2001 write) ─────────────
    private bool _renderEnabled;
    private bool _showBg;
    private bool _showBgLeft8;
    private bool _showSprites;
    private bool _showSpriteLeft8;
    private bool _patternBg;
    private bool _patternSprite;
    private bool _spriteSize16;
    private readonly PpuBackgroundScanlineState[] _backgroundScanlineStates = new PpuBackgroundScanlineState[NesConstants.ScreenHeight];
    private bool _hasCapturedBackgroundScanlineStates;

    // ── Cartridge ref ─────────────────────────────────────────────────────
    private ICartridge? _cart;
    public void InsertCartridge(ICartridge cart) => _cart = cart;

    private IPpuNametableProvider? NametableProvider =>
        _cart is MapperCartridge mapperCartridge
            ? mapperCartridge.PpuNametableProvider
            : _cart as IPpuNametableProvider;

    private IPpuAddressObserver? PpuAddressObserver =>
        _cart is MapperCartridge mapperCartridge
            ? mapperCartridge.PpuAddressObserver
            : _cart as IPpuAddressObserver;

    // ── NES palette (ARGB) ────────────────────────────────────────────────
    private static readonly uint[] NesPalette = {
        0xFF545454,0xFF001E74,0xFF08102C,0xFF300088,0xFF440064,0xFF5C0030,0xFF540400,0xFF3C1800,
        0xFF202A00,0xFF083A00,0xFF004000,0xFF003C00,0xFF00323C,0xFF000000,0xFF000000,0xFF000000,
        0xFF989698,0xFF084CC4,0xFF3032EC,0xFF5C1EE4,0xFF8814B0,0xFFA01464,0xFF982220,0xFF783C00,
        0xFF545A00,0xFF287200,0xFF087C00,0xFF007628,0xFF006678,0xFF000000,0xFF000000,0xFF000000,
        0xFFECEEEC,0xFF4C9AEC,0xFF787CEC,0xFFB062EC,0xFFE454EC,0xFFEC58B4,0xFFEC6A64,0xFFD48820,
        0xFFA0AA00,0xFF74C400,0xFF4CD020,0xFF38CC6C,0xFF38B4CC,0xFF3C3C3C,0xFF000000,0xFF000000,
        0xFFECEEEC,0xFFA8CCEC,0xFFBCBCEC,0xFFD4B2EC,0xFFECAEEC,0xFFECAED4,0xFFECB4B0,0xFFE4C490,
        0xFFCCD278,0xFFB4DE78,0xFFA8E290,0xFF98E2B4,0xFFA0D6E4,0xFFA0A2A0,0xFF000000,0xFF000000,
    };

    // ─────────────────────────────────────────────────────────────────────
    //  Clock (one PPU cycle)
    // ─────────────────────────────────────────────────────────────────────

    public void Clock()
    {
        if (Scanline >= -1 && Scanline < 240)
        {
            // ── Background ──────────────────────────────────────────────
            if (Scanline == -1 && Cycle == 1)
            {
                Status &= ~PpuStatus.VerticalBlank;
                Status &= ~PpuStatus.Sprite0Hit;
                Status &= ~PpuStatus.SpriteOverflow;
                Array.Clear(_spriteShiftLo); Array.Clear(_spriteShiftHi);
            }

            if ((Cycle >= 2 && Cycle < 258) || (Cycle >= 321 && Cycle < 338))
            {
                ShiftBackground();
                FetchBackground();
            }

            if (Cycle == 256 && _renderEnabled) IncrementScrollY();
            if (Cycle == 257 && _renderEnabled) { CopyScrollX(); EvaluateSprites(); }
            if (Scanline == -1 && Cycle >= 280 && Cycle < 305 && _renderEnabled) CopyScrollY();


            if (Cycle == 337 || Cycle == 339) _ = ReadVram((ushort)(0x2000 | (_v & 0x0FFF)));

        }

        if (Scanline == 241 && Cycle == 1)
        {
            Status |= PpuStatus.VerticalBlank;
            if (Control.HasFlag(PpuControl.NmiEnable)) NmiPending = true;
        }

        if (Scanline >= 0 && Scanline < NesConstants.ScreenHeight && Cycle == 1)
        {
            _hasCapturedBackgroundScanlineStates = true;
            ushort visibleScanlineStartV = RewindScrollXPrefetch(_v);
            int visibleFineScrollX = _x + 1;
            if (visibleFineScrollX >= 8)
            {
                visibleFineScrollX -= 8;
                visibleScanlineStartV = AdvanceScrollX(visibleScanlineStartV);
            }

            _backgroundScanlineStates[Scanline] = new PpuBackgroundScanlineState
            {
                FineScrollX = visibleFineScrollX,
                FineScrollY = (visibleScanlineStartV >> 12) & 0x07,
                CoarseScrollX = visibleScanlineStartV & 0x1F,
                CoarseScrollY = (visibleScanlineStartV >> 5) & 0x1F,
                NametableSelect = (visibleScanlineStartV >> 10) & 0x03,
                UseBackgroundPatternTableHighBank = _patternBg,
                ShowBackground = _showBg,
                ShowBackgroundLeft8 = _showBgLeft8
            };
        }

        // ── Pixel output ────────────────────────────────────────────────
        if (Scanline >= 0 && Scanline < 240 && Cycle >= 1 && Cycle <= 256)
        {
            byte bgPixel = 0, bgPalette = 0;
            byte fgPixel = 0, fgPalette = 0, fgPriority = 0;
            bool sprite0Rendered = false;

            if (_showBg && (_showBgLeft8 || Cycle > 8))
            {
                ushort bit = (ushort)(0x8000 >> _x);
                bgPixel   = (byte)(((_bgShiftHi & bit) != 0 ? 2 : 0) | ((_bgShiftLo & bit) != 0 ? 1 : 0));
                bgPalette = (byte)(((_atShiftHi & bit) != 0 ? 2 : 0) | ((_atShiftLo & bit) != 0 ? 1 : 0));
            }

            if (_showSprites && (_showSpriteLeft8 || Cycle > 8))
            {
                for (int i = 0; i < _spriteCount; i++)
                {
                    if (_spriteX[i] == 0)
                    {
                        byte hi = (byte)((_spriteShiftHi[i] & 0x80) != 0 ? 2 : 0);
                        byte lo = (byte)((_spriteShiftLo[i] & 0x80) != 0 ? 1 : 0);
                        fgPixel   = (byte)(hi | lo);
                        fgPalette = (byte)((_spriteAttr[i] & 0x03) + 4);
                        fgPriority = (byte)((_spriteAttr[i] & 0x20) == 0 ? 1 : 0);
                        if (fgPixel != 0) { if (i == 0) sprite0Rendered = true; break; }
                    }
                }
            }

            byte pixel = 0, palette = 0;
            if (bgPixel == 0 && fgPixel == 0) { pixel = 0; palette = 0; }
            else if (bgPixel == 0)             { pixel = fgPixel; palette = fgPalette; }
            else if (fgPixel == 0)             { pixel = bgPixel; palette = bgPalette; }
            else
            {
                if (sprite0Rendered && _sprite0Rendering && _showBg && _showSprites && Cycle < 256)
                    Status |= PpuStatus.Sprite0Hit;
                pixel   = fgPriority != 0 ? fgPixel   : bgPixel;
                palette = fgPriority != 0 ? fgPalette : bgPalette;
            }

            // Inline palette read — avoid ReadVram dispatch overhead
            int palIdx = ((palette << 2) + pixel) & 0x1F;
            if ((palIdx & 0x13) == 0x10) palIdx &= 0x0F;
            FrameBuffer[Scanline * 256 + (Cycle - 1)] = NesPalette[PaletteRam[palIdx] & 0x3F];
        }

        // ── Advance counters ────────────────────────────────────────────
        Cycle++;
        if (_renderEnabled && Scanline == -1 && Cycle == 340 && (Frame & 1) == 1)
            Cycle++;

        // Signal mapper scanline IRQ at cycle 260 of each rendered line (not pre-render line)
        if (_renderEnabled && Cycle == 260 && Scanline >= 0 && Scanline < 240)
            _cart?.SignalScanline();

        if (Cycle > 340)
        {
            Cycle = 0;
            Scanline++;
            if (Scanline > 260)
            {
                Scanline = -1;
                Frame++;
                FrameComplete = true;
                NmiPending = false;
            }
        }
    }

    public void Reset()
    {
        Control = 0; Mask = 0; Status = 0;
        _v = _t = 0; _x = 0; _w = false;
        Scanline = -1; Cycle = 0; Frame = 0;
        _ppuDataBuf = 0; NmiPending = false;
        _hasCapturedBackgroundScanlineStates = false;
        Array.Clear(Vram); Array.Clear(PaletteRam); Array.Clear(Oam);
        Array.Clear(_backgroundScanlineStates);
    }

}
