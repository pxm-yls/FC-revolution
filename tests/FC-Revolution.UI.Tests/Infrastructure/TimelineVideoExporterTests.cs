using System.Buffers.Binary;
using FCRevolution.Storage;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class TimelineVideoExporterTests
{
    [Fact]
    public void BuildReplayPlan_UsesSnapshotFrameAndFiltersRecords()
    {
        var inputLogPath = Path.Combine(Path.GetTempPath(), $"export-plan-{Guid.NewGuid():N}.bin");

        try
        {
            using (var writer = new ReplayLogWriter())
            {
                writer.Open(inputLogPath, resetFile: true);
                writer.Append(new FrameInputRecord(119, 0x01, 0x00));
                writer.Append(new FrameInputRecord(120, 0x02, 0x00));
                writer.Append(new FrameInputRecord(121, 0x03, 0x00));
                writer.Append(new FrameInputRecord(124, 0x04, 0x00));
                writer.Append(new FrameInputRecord(130, 0x05, 0x00));
                writer.Flush();
            }

            var snapshotBytes = CreateSnapshotBytes(frame: 120);

            var plan = TimelineVideoExporter.BuildReplayPlan(snapshotBytes, inputLogPath, 122, 124);

            Assert.Equal(120, plan.BaseFrame);
            Assert.Collection(plan.Records,
                first => Assert.Equal(121, first.Frame),
                second => Assert.Equal(124, second.Frame));
        }
        finally
        {
            if (File.Exists(inputLogPath))
                File.Delete(inputLogPath);
        }
    }

    [Fact]
    public void BuildReplayPlan_UsesStartFrameForLegacySnapshots()
    {
        var inputLogPath = Path.Combine(Path.GetTempPath(), $"legacy-export-plan-{Guid.NewGuid():N}.bin");

        try
        {
            using (var writer = new ReplayLogWriter())
            {
                writer.Open(inputLogPath, resetFile: true);
                writer.Append(new FrameInputRecord(59, 0x01, 0x00));
                writer.Append(new FrameInputRecord(60, 0x02, 0x00));
                writer.Append(new FrameInputRecord(61, 0x03, 0x00));
                writer.Flush();
            }

            var plan = TimelineVideoExporter.BuildReplayPlan([0x01, 0x02, 0x03], inputLogPath, 60, 61);

            Assert.Equal(60, plan.BaseFrame);
            Assert.Single(plan.Records);
            Assert.Equal(61, plan.Records[0].Frame);
        }
        finally
        {
            if (File.Exists(inputLogPath))
                File.Delete(inputLogPath);
        }
    }

    private static byte[] CreateSnapshotBytes(long frame)
    {
        var bytes = new byte[14];
        "FCRS"u8.CopyTo(bytes);
        bytes[4] = 1;
        bytes[5] = 0;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(6, 8), frame);
        return bytes;
    }
}
