using FCRevolution.Core.Mappers;

namespace FC_Revolution.Core.Tests;

public class MapperSerializationTests
{
    // ── Mapper000 ─────────────────────────────────────────────────────────

    [Fact]
    public void Mapper000_ChrRam_SerializeRoundtrip()
    {
        var rom  = BuildNrom(prgBanks: 2, chrBanks: 0); // CHR-RAM
        var m    = CreateMapper(rom);

        m.PpuWrite(0x0010, 0xAB);
        var state = m.SerializeState();
        m.PpuWrite(0x0010, 0x00);
        m.DeserializeState(state);

        Assert.Equal(0xAB, m.PpuRead(0x0010));
    }

    [Fact]
    public void Mapper000_PrgRead_CorrectBank()
    {
        var rom = BuildNrom(prgBanks: 1, chrBanks: 1);
        // 1×16KB bank: $FFFC maps to offset 0x3FFC within the bank
        rom[16 + 0x3FFC] = 0x34;
        rom[16 + 0x3FFD] = 0x12;
        var m = CreateMapper(rom);
        Assert.Equal(0x34, m.CpuRead(0xFFFC));
        Assert.Equal(0x12, m.CpuRead(0xFFFD));
    }

    // ── Mapper002 ─────────────────────────────────────────────────────────

    [Fact]
    public void Mapper002_BankSwitch_SerializeRoundtrip()
    {
        var rom = BuildUxrom(prgBanks: 4);
        var m   = CreateMapper(rom);

        m.CpuWrite(0x8000, 2); // switch to bank 2
        var state = m.SerializeState();

        m.CpuWrite(0x8000, 0); // reset
        m.DeserializeState(state);

        // Bank 2 should be at $8000–$BFFF
        // Writes to bank register store the bank index
        var stateCheck = m.SerializeState();
        Assert.Equal(2, stateCheck[0]);
    }

    // ── Mapper003 ─────────────────────────────────────────────────────────

    [Fact]
    public void Mapper003_ChrBank_SerializeRoundtrip()
    {
        var rom = BuildCnrom(chrBanks: 4);
        var m   = CreateMapper(rom);

        m.CpuWrite(0x8000, 3); // CHR bank 3
        var state = m.SerializeState();

        m.CpuWrite(0x8000, 0);
        m.DeserializeState(state);

        var check = m.SerializeState();
        Assert.Equal(3, check[0]);
    }

    // ── Mapper004 ─────────────────────────────────────────────────────────

    [Fact]
    public void Mapper004_BankRegisters_SerializeRoundtrip()
    {
        var rom = BuildMmc3(prgBanks: 4, chrBanks: 8);
        var m   = CreateMapper(rom);

        // Set bank register 6 to value 2 (PRG bank)
        m.CpuWrite(0x8000, 6);    // select bank register 6
        m.CpuWrite(0x8001, 2);    // set value

        var state = m.SerializeState();

        m.CpuWrite(0x8001, 0);    // clear
        m.DeserializeState(state);

        var check = m.SerializeState();
        // Bank reg 6 is at bytes [6*4 .. 6*4+3]
        int bankReg6 = BitConverter.ToInt32(check, 6 * 4);
        Assert.Equal(2, bankReg6);
    }

    [Fact]
    public void Mapper004_IrqState_SerializeRoundtrip()
    {
        var rom = BuildMmc3(prgBanks: 4, chrBanks: 8);
        var m   = CreateMapper(rom);

        m.CpuWrite(0xC000, 5);    // IRQ period = 5
        m.CpuWrite(0xC001, 0);    // reload
        m.CpuWrite(0xE001, 0);    // enable IRQ

        var state = m.SerializeState();
        m.Reset();
        m.DeserializeState(state);

        var check = m.SerializeState();
        int baseOff = 8 * 4;
        byte flags = check[baseOff + 1];
        Assert.True((flags & 4) != 0, "IRQ enable should be set after roundtrip");
    }

    [Fact]
    public void Mapper004_SignalScanline_TriggersIrq()
    {
        var rom = BuildMmc3(prgBanks: 4, chrBanks: 8);
        var m   = CreateMapper(rom);

        // Hardware: signal 1 reloads counter to period; signal (period+1) fires IRQ.
        // With period=1: signal 1 reloads→1, signal 2 decrements→0 → fires.
        m.CpuWrite(0xC000, 1);    // IRQ period = 1
        m.CpuWrite(0xC001, 0);    // schedule reload
        m.CpuWrite(0xE001, 0);    // enable IRQ

        Assert.False(m.IrqActive);
        m.SignalScanline(); // reload: counter = 1
        Assert.False(m.IrqActive);
        m.SignalScanline(); // counter-- → 0 → fires
        Assert.True(m.IrqActive);
    }

    [Fact]
    public void Mapper001_BankRegs_SerializeRoundtrip()
    {
        var rom = BuildMmc1(prgBanks: 4, chrBanks: 4);
        var m   = CreateMapper(rom);

        // Shift in CHR bank 0 = 2 via shift register
        WriteMMC1(m, 0xA000, 2); // CHR bank 0
        var state = m.SerializeState();

        m.Reset();
        m.DeserializeState(state);

        var check = m.SerializeState();
        Assert.Equal(2, check[2]); // _chrBank0 at index 2
    }

    [Fact]
    public void Mapper015_Mode0_MapsContiguousPrgBanks()
    {
        var rom = BuildRom(15, prgBanks16: 64, chrBanks8: 0);
        StampPrgBanks(rom, bankSize: 8192);
        var m = CreateMapper(rom);

        m.CpuWrite(0x8000, 0x01);

        Assert.Equal(2, m.CpuRead(0x8000));
        Assert.Equal(3, m.CpuRead(0xA000));
        Assert.Equal(4, m.CpuRead(0xC000));
        Assert.Equal(5, m.CpuRead(0xE000));
    }

    [Fact]
    public void Mapper025_AdvanceCpuCycles_TriggersIrq()
    {
        var rom = BuildRom(25, prgBanks16: 16, chrBanks8: 32);
        var cart = CreateMapper(rom);
        var cycleDriven = Assert.IsAssignableFrom<ICpuCycleDrivenMapper>(cart);

        cart.CpuWrite(0xF000, 0x01);
        cart.CpuWrite(0xF002, 0x00);
        cart.CpuWrite(0xF001, 0x06);

        cycleDriven.AdvanceCpuCycles(256);

        Assert.True(cart.IrqActive);
    }

    [Fact]
    public void Mapper068_ChrBackedNametable_ReadsThroughProvider()
    {
        var rom = BuildRom(68, prgBanks16: 8, chrBanks8: 16);
        StampChrBanks(rom, bankSize: 1024);
        var cart = CreateMapper(rom);
        var nametableProvider = Assert.IsAssignableFrom<IPpuNametableProvider>(cart);

        cart.CpuWrite(0xC000, 0x03);
        cart.CpuWrite(0xE000, 0x12);

        Assert.True(nametableProvider.TryReadNametable(0x2000, out var data));
        Assert.Equal(3, data);
    }

    [Fact]
    public void Mapper073_AdvanceCpuCycles_TriggersIrq()
    {
        var rom = BuildRom(73, prgBanks16: 8, chrBanks8: 0);
        var cart = CreateMapper(rom);
        var cycleDriven = Assert.IsAssignableFrom<ICpuCycleDrivenMapper>(cart);

        cart.CpuWrite(0x8000, 0x0F);
        cart.CpuWrite(0x9000, 0x0F);
        cart.CpuWrite(0xC000, 0x06);

        cycleDriven.AdvanceCpuCycles(1);

        Assert.True(cart.IrqActive);
    }

    [Fact]
    public void Mapper090_PrgMode2_MapsIndependentBanks()
    {
        var rom = BuildRom(90, prgBanks16: 16, chrBanks8: 32);
        StampPrgBanks(rom, bankSize: 8192);
        var cart = CreateMapper(rom);

        cart.CpuWrite(0xD000, 0x02);
        cart.CpuWrite(0x8000, 0x01);
        cart.CpuWrite(0x8001, 0x02);
        cart.CpuWrite(0x8002, 0x03);

        Assert.Equal(1, cart.CpuRead(0x8000));
        Assert.Equal(2, cart.CpuRead(0xA000));
        Assert.Equal(3, cart.CpuRead(0xC000));
    }

    [Fact]
    public void Mapper074_ChrBanks8And9_UseExtraChrRam()
    {
        var rom = BuildRom(74, prgBanks16: 32, chrBanks8: 32);
        var m = CreateMapper(rom);

        m.CpuWrite(0x8000, 0x00);
        m.CpuWrite(0x8001, 0x08);

        m.PpuWrite(0x0000, 0xAB);
        m.PpuWrite(0x0400, 0xCD);

        Assert.Equal(0xAB, m.PpuRead(0x0000));
        Assert.Equal(0xCD, m.PpuRead(0x0400));
    }

    [Fact]
    public void Mapper087_ChrLatch_SelectsSwizzledBank()
    {
        var rom = BuildRom(87, prgBanks16: 2, chrBanks8: 4);
        StampChrBanks(rom, bankSize: 8192);
        var m = CreateMapper(rom);

        m.CpuWrite(0x6000, 0x01);

        Assert.Equal(2, m.PpuRead(0x0000));
    }

    [Fact]
    public void Mapper163_Readback_AndScanlineChrSwitch_Work()
    {
        var rom = BuildRom(163, prgBanks16: 64, chrBanks8: 1);
        StampChrHalfBanks(rom);
        var m = CreateMapper(rom);

        m.CpuWrite(0x5300, 0x05);
        m.CpuWrite(0x5000, 0x01);
        m.CpuWrite(0x5101, 0x01);
        m.CpuWrite(0x5101, 0x00);

        Assert.Equal(0x05, m.CpuRead(0x5500));

        m.CpuWrite(0x5000, 0x80);
        for (int i = 0; i < 128; i++)
            m.SignalScanline();

        Assert.Equal(0x22, m.PpuRead(0x0000));
    }

    [Fact]
    public void Mapper164_Selects32kPrgBank()
    {
        var rom = BuildRom(164, prgBanks16: 64, chrBanks8: 0);
        StampPrgBanks(rom, bankSize: 32768);
        var m = CreateMapper(rom);

        m.CpuWrite(0x5100, 0x01);
        m.CpuWrite(0x5000, 0x02);

        Assert.Equal(18, m.CpuRead(0x8000));
    }

    [Fact]
    public void Mapper240_LatchedValue_SelectsPrgAndChrBanks()
    {
        var rom = BuildRom(240, prgBanks16: 16, chrBanks8: 16, flags6Extra: 0x02);
        StampPrgBanks(rom, bankSize: 32768);
        StampChrBanks(rom, bankSize: 8192);
        var m = CreateMapper(rom);

        m.CpuWrite(0x4020, 0x21);

        Assert.Equal(2, m.CpuRead(0x8000));
        Assert.Equal(1, m.PpuRead(0x0000));
    }

    [Fact]
    public void Mapper242_LatchedAddress_SelectsPrgBank()
    {
        var rom = BuildRom(242, prgBanks16: 32, chrBanks8: 0);
        StampPrgBanks(rom, bankSize: 32768);
        var m = CreateMapper(rom);

        m.CpuWrite(0x802A, 0xFF);

        Assert.Equal(5, m.CpuRead(0x8000));
    }

    [Fact]
    public void Mapper245_ChrRegisterBit_SelectsPrgHighBank()
    {
        var rom = BuildRom(245, prgBanks16: 64, chrBanks8: 0, flags6Extra: 0x02);
        StampPrgBanks(rom, bankSize: 8192);
        var m = CreateMapper(rom);

        m.CpuWrite(0x8000, 0x06);
        m.CpuWrite(0x8001, 0x00);
        Assert.Equal(0, m.CpuRead(0x8000));

        m.CpuWrite(0x8000, 0x00);
        m.CpuWrite(0x8001, 0x02);

        Assert.Equal(64, m.CpuRead(0x8000));
    }

    [Fact]
    public void Mapper246_RegisterWindow_SelectsIndependentBanks()
    {
        var rom = BuildRom(246, prgBanks16: 32, chrBanks8: 16, flags6Extra: 0x02);
        StampPrgBanks(rom, bankSize: 8192);
        StampChrBanks(rom, bankSize: 2048);
        var m = CreateMapper(rom);

        m.CpuWrite(0x6000, 0x02);
        m.CpuWrite(0x6001, 0x03);
        m.CpuWrite(0x6004, 0x01);

        Assert.Equal(2, m.CpuRead(0x8000));
        Assert.Equal(3, m.CpuRead(0xA000));
        Assert.Equal(1, m.PpuRead(0x0000));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ICartridge CreateMapper(byte[] rom) => MapperFactory.Create(rom);

    private static byte[] BuildRom(int mapperNumber, int prgBanks16, int chrBanks8, byte flags6Extra = 0x00)
    {
        int prgSize = prgBanks16 * 16384;
        int chrSize = chrBanks8 * 8192;
        var rom = new byte[16 + prgSize + chrSize];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = (byte)prgBanks16;
        rom[5] = (byte)chrBanks8;
        rom[6] = (byte)(((mapperNumber & 0x0F) << 4) | (flags6Extra & 0x0F));
        rom[7] = (byte)(mapperNumber & 0xF0);
        return rom;
    }

    private static void StampPrgBanks(byte[] rom, int bankSize)
    {
        int prgSize = rom[4] * 16384;
        int bankCount = prgSize / bankSize;
        int baseOffset = 16;
        for (int bank = 0; bank < bankCount; bank++)
            rom[baseOffset + (bank * bankSize)] = (byte)bank;
    }

    private static void StampChrBanks(byte[] rom, int bankSize)
    {
        int prgSize = rom[4] * 16384;
        int chrSize = rom[5] * 8192;
        int bankCount = chrSize / bankSize;
        int baseOffset = 16 + prgSize;
        for (int bank = 0; bank < bankCount; bank++)
            rom[baseOffset + (bank * bankSize)] = (byte)bank;
    }

    private static void StampChrHalfBanks(byte[] rom)
    {
        int prgSize = rom[4] * 16384;
        int baseOffset = 16 + prgSize;
        rom[baseOffset] = 0x11;
        rom[baseOffset + 0x1000] = 0x22;
    }

    private static byte[] BuildNrom(int prgBanks, int chrBanks)
    {
        return BuildRom(0, prgBanks, chrBanks);
    }

    private static byte[] BuildUxrom(int prgBanks)
    {
        return BuildRom(2, prgBanks, 0);
    }

    private static byte[] BuildCnrom(int chrBanks)
    {
        return BuildRom(3, 1, chrBanks);
    }

    private static byte[] BuildMmc3(int prgBanks, int chrBanks)
    {
        return BuildRom(4, prgBanks * 8192 / 16384, chrBanks * 1024 / 8192);
    }

    private static byte[] BuildMmc1(int prgBanks, int chrBanks)
    {
        return BuildRom(1, prgBanks, chrBanks * 4096 / 8192);
    }

    /// <summary>Write a 5-bit value to an MMC1 register via serial bit writes.</summary>
    private static void WriteMMC1(ICartridge m, ushort addr, byte val)
    {
        for (int i = 0; i < 5; i++)
        {
            byte bit = (byte)((val >> i) & 1);
            m.CpuWrite(addr, bit);
        }
    }
}
