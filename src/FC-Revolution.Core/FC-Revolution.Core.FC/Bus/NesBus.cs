using FCRevolution.Core.APU;
using FCRevolution.Core.Input;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;

namespace FCRevolution.Core.Bus;

public sealed class NesBus : IBus
{
    private readonly byte[] _ram = new byte[2048];

    // Components (set after construction)
    public Ppu2C02?    Ppu       { get; set; }
    public Apu2A03?    Apu       { get; set; }
    public ICartridge? Cartridge { get; set; }
    public IController Controller1 { get; set; } = new StandardController();
    public IController Controller2 { get; set; } = new StandardController();

    // OAM DMA state
    private bool   _dmaPending;
    private byte   _dmaPage;
    private int    _dmaCycle;
    public  bool   DmaActive => _dmaPending;

    public byte Read(ushort address)
    {
        if (address < 0x2000)
            return _ram[address & 0x07FF];

        if (address < 0x4000)
            return Ppu?.ReadRegister((ushort)(0x2000 + (address & 0x0007))) ?? 0;

        if (address == 0x4015)
            return Apu?.ReadRegister(address) ?? 0;

        if (address == 0x4016)
            return Controller1.ReadState();

        if (address == 0x4017)
            return Controller2.ReadState();

        if (address >= 0x4020)
            return Cartridge?.CpuRead(address) ?? 0;

        return 0;
    }

    public void Write(ushort address, byte data)
    {
        if (address < 0x2000) { _ram[address & 0x07FF] = data; return; }

        if (address < 0x4000) { Ppu?.WriteRegister((ushort)(0x2000 + (address & 0x0007)), data); return; }

        if (address == 0x4014) { _dmaPage = data; _dmaPending = true; _dmaCycle = 0; return; }

        if (address == 0x4016) { Controller1.Write(data); Controller2.Write(data); return; }

        if (address >= 0x4000 && address < 0x4018) { Apu?.WriteRegister(address, data); return; }

        if (address >= 0x4020) Cartridge?.CpuWrite(address, data);
    }

    /// <summary>Returns true while OAM DMA is still transferring.</summary>
    public bool ClockDma()
    {
        if (!_dmaPending) return false;
        if (_dmaCycle < 512)
        {
            if ((_dmaCycle & 1) == 0)
            {
                byte d = Read((ushort)((_dmaPage << 8) | (_dmaCycle >> 1)));
                Ppu?.WriteRegister(0x2004, d);
            }
            _dmaCycle++;
            return true;
        }
        _dmaPending = false;
        return false;
    }

    public byte[] GetRam() => _ram;
}
