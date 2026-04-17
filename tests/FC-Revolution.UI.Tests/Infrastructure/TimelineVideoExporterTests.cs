using System;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class TimelineVideoExporterTests
{
    [Fact]
    public void ExportMp4_ThrowsFriendlyError_WhenLegacyRendererUnavailable()
    {
        var snapshotPath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(snapshotPath, [0x01, 0x02, 0x03]);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                TimelineVideoExporter.ExportMp4(
                    "demo.nes",
                    snapshotPath,
                    "/tmp/demo.fcrr",
                    0,
                    1,
                    "/tmp/demo.mp4",
                    new FakeLegacyFeatureRuntime
                    {
                        ErrorMessage = "legacy renderer missing"
                    }));

            Assert.Contains("legacy renderer missing", exception.Message);
        }
        finally
        {
            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);
        }
    }
}
