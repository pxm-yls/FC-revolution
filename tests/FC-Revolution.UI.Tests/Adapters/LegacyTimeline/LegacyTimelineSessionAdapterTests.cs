using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;

namespace FC_Revolution.UI.Tests;

public sealed class LegacyTimelineSessionAdapterTests
{
    [Fact]
    public void Initialize_WhenLegacyTimelineRuntimeUnavailable_ReturnsEmptyState()
    {
        var romPath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);
            var adapter = new LegacyTimelineSessionAdapter(
                new CoreBranchTree(),
                new FakeLegacyFeatureRuntime
                {
                    ErrorMessage = "timeline bridge missing"
                });

            var state = adapter.Initialize(
                romPath,
                "Demo",
                loadFullTimeline: true,
                previewWidth: 256,
                previewHeight: 240);

            Assert.Empty(state.PreviewNodes);
            Assert.False(adapter.IsTimelineLoaded);
            Assert.False(adapter.TryReload(256, 240).HasValue);
        }
        finally
        {
            if (File.Exists(romPath))
                File.Delete(romPath);
        }
    }
}
