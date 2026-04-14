using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

public sealed class CoreMediaFilePatternCatalogTests
{
    [Fact]
    public void ResolvePatterns_WhenNoManifestMetadata_FallsBackToNesPattern()
    {
        var patterns = CoreMediaFilePatternCatalog.ResolvePatterns(
            [
                new CoreManifest("core.a", "Core A", "a", "1.0.0", CoreBinaryKinds.ManagedDotNet),
                new CoreManifest("core.b", "Core B", "b", "1.0.0", CoreBinaryKinds.ManagedDotNet)
            ]);

        Assert.Equal(["*.nes"], patterns);
    }

    [Fact]
    public void ResolvePatterns_NormalizesDistinctManifestPatterns()
    {
        var patterns = CoreMediaFilePatternCatalog.ResolvePatterns(
            [
                new CoreManifest("core.a", "Core A", "a", "1.0.0", CoreBinaryKinds.ManagedDotNet)
                {
                    SupportedMediaFilePatterns = [".sfc", "*.smc", "*.SFC"]
                },
                new CoreManifest("core.b", "Core B", "b", "1.0.0", CoreBinaryKinds.ManagedDotNet)
                {
                    SupportedMediaFilePatterns = ["bin", "*.zip"]
                }
            ]);

        Assert.Equal(["*.bin", "*.sfc", "*.smc", "*.zip"], patterns);
    }
}
