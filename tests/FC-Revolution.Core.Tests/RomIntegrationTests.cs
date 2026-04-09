using FCRevolution.Core;
using FCRevolution.Core.Mappers;

namespace FC_Revolution.Core.Tests;

public class RomIntegrationTests
{
    private const string SuperMarioPath = "/Users/pxm/Desktop/Cs/FC/Super Mario.nes";

    // ── Mapper factory ────────────────────────────────────────────────────

    [Fact]
    public void MapperFactory_ThrowsOnInvalidHeader()
    {
        Assert.Throws<InvalidDataException>(() => MapperFactory.Create(new byte[16]));
    }

    [Fact]
    public void MapperFactory_ThrowsOnUnsupportedMapper()
    {
        // Craft a minimal iNES header with mapper 7 (not implemented)
        var rom = new byte[16 + 16384 + 8192];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = 1; rom[5] = 1;
        rom[6] = 0x70; // mapper 7 low nibble = 7
        Assert.Throws<NotSupportedException>(() => MapperFactory.Create(rom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(25)]
    [InlineData(68)]
    [InlineData(73)]
    [InlineData(74)]
    [InlineData(87)]
    [InlineData(90)]
    [InlineData(163)]
    [InlineData(164)]
    [InlineData(240)]
    [InlineData(242)]
    [InlineData(245)]
    [InlineData(246)]
    public void MapperFactory_CreatesKnownMappers(int mapperNum)
    {
        int prgBanks = mapperNum switch
        {
            0 => 1,
            163 or 164 or 245 => 64,
            74 => 32,
            25 => 16,
            68 => 8,
            73 => 8,
            90 => 16,
            _ => 2,
        };
        int chrBanks = mapperNum switch
        {
            15 or 73 or 163 or 164 or 242 or 245 => 0,
            74 => 32,
            25 => 32,
            68 => 16,
            90 => 32,
            240 => 16,
            246 => 16,
            _ => 1,
        };

        var rom = new byte[16 + prgBanks * 16384 + chrBanks * 8192];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = (byte)prgBanks; rom[5] = (byte)chrBanks;
        rom[6] = (byte)((mapperNum & 0x0F) << 4);
        rom[7] = (byte)(mapperNum & 0xF0);
        var cart = MapperFactory.Create(rom);
        Assert.Equal(mapperNum, cart.MapperNumber);
    }

    [Fact]
    public void MapperFactory_RegisteredMappers_AreProfileBacked()
    {
        Assert.Equal("NROM", MapperFactory.RegisteredMappers[0]);
        Assert.Equal("MMC1 (SxROM)", MapperFactory.RegisteredMappers[1]);
        Assert.Equal("MMC3 (TxROM)", MapperFactory.RegisteredMappers[4]);
        Assert.Equal("Mapper 245", MapperFactory.RegisteredMappers[245]);
    }

    // ── Super Mario Bros ROM ───────────────────────────────────────────────

    [SkippableFact]
    public void SuperMario_Loads_WithoutException()
    {
        Skip.If(!File.Exists(SuperMarioPath), "Super Mario.nes not found");

        var nes = new NesConsole();
        nes.LoadRom(SuperMarioPath);
        Assert.True(nes.Running);
        Assert.NotNull(nes.Cartridge);
    }

    [SkippableFact]
    public void SuperMario_RunsFor10Frames_WithoutException()
    {
        Skip.If(!File.Exists(SuperMarioPath), "Super Mario.nes not found");

        var nes = new NesConsole();
        nes.LoadRom(SuperMarioPath);

        for (int i = 0; i < 10; i++)
        {
            var fb = nes.RunFrame();
            Assert.NotNull(fb);
            Assert.Equal(256 * 240, fb.Length);
        }

        Assert.True(nes.Ppu.Frame >= 10);
    }

    [SkippableFact]
    public void SuperMario_FrameBufferChanges_AfterReset()
    {
        Skip.If(!File.Exists(SuperMarioPath), "Super Mario.nes not found");

        var nes = new NesConsole();
        nes.LoadRom(SuperMarioPath);

        // Run 5 frames
        for (int i = 0; i < 5; i++) nes.RunFrame();
        var fb1 = (uint[])nes.Ppu.FrameBuffer.Clone();

        // Run 5 more
        for (int i = 0; i < 5; i++) nes.RunFrame();
        var fb2 = nes.Ppu.FrameBuffer;

        // Frames should not be identical (PPU is producing output)
        bool different = false;
        for (int i = 0; i < fb1.Length; i++)
            if (fb1[i] != fb2[i]) { different = true; break; }

        Assert.True(different, "Frame buffer did not change after 5 frames");
    }

    [SkippableFact]
    public void SuperMario_SaveLoad_State_RestoresCpuRegisters()
    {
        Skip.If(!File.Exists(SuperMarioPath), "Super Mario.nes not found");

        var nes = new NesConsole();
        nes.LoadRom(SuperMarioPath);

        // Advance to a non-trivial state
        for (int i = 0; i < 30; i++) nes.RunFrame();

        byte cpuA = nes.Cpu.A;
        byte cpuX = nes.Cpu.X;
        ushort pc  = nes.Cpu.PC;

        byte[] snapshot = nes.SaveState();

        // Run more frames
        for (int i = 0; i < 10; i++) nes.RunFrame();

        // Restore
        nes.LoadState(snapshot);

        Assert.Equal(cpuA, nes.Cpu.A);
        Assert.Equal(cpuX, nes.Cpu.X);
        Assert.Equal(pc,   nes.Cpu.PC);
    }
}
