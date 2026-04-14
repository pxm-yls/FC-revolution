using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

public sealed class RomMapperInspectorTests
{
    [Fact]
    public void Inspect_ReturnsRegisteredMapperInfo_ForSupportedMapper()
    {
        var romPath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(romPath, BuildHeader(mapperNumber: 4));

            var mapper = RomMapperInspector.Inspect(romPath);

            Assert.True(mapper.IsSupported);
            Assert.Equal(4, mapper.Number);
            Assert.Contains("MMC3", mapper.Name);
        }
        finally
        {
            File.Delete(romPath);
        }
    }

    [Fact]
    public void Inspect_ReturnsUnknownInfo_ForUnsupportedMapper()
    {
        var romPath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(romPath, BuildHeader(mapperNumber: 99));

            var mapper = RomMapperInspector.Inspect(romPath);

            Assert.False(mapper.IsSupported);
            Assert.Equal(99, mapper.Number);
            Assert.Equal("未支持/未知核心", mapper.Name);
        }
        finally
        {
            File.Delete(romPath);
        }
    }

    [Fact]
    public void Inspect_ReturnsUnavailableInfo_WhenLegacyInspectorIsMissing()
    {
        var mapper = RomMapperInspector.Inspect(
            "/tmp/missing.nes",
            new FakeLegacyFeatureRuntime
            {
                ErrorMessage = "legacy bridge missing"
            });

        Assert.False(mapper.IsSupported);
        Assert.Equal(-1, mapper.Number);
        Assert.Equal("Mapper 信息不可用", mapper.Name);
        Assert.Equal("Mapper 信息不可用", mapper.DisplayLabel);
    }

    private static byte[] BuildHeader(int mapperNumber)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)'N';
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'S';
        bytes[3] = 0x1A;
        bytes[4] = 1;
        bytes[5] = 1;
        bytes[6] = (byte)((mapperNumber & 0x0F) << 4);
        bytes[7] = (byte)(mapperNumber & 0xF0);
        return bytes;
    }
}
