using FCRevolution.Rendering.Common;

namespace FC_Revolution.Rendering.Tests;

public sealed class FrameDoubleBufferTests
{
    [Fact]
    public void ReadFront_ReturnsNull_BeforeAnySwap()
    {
        var buffer = new FrameDoubleBuffer<FrameToken>();

        Assert.Null(buffer.ReadFront());
    }

    [Fact]
    public void WriteBack_DoesNotExposeFrame_UntilSwap()
    {
        var buffer = new FrameDoubleBuffer<FrameToken>();
        var token = new FrameToken("back");

        buffer.WriteBack(token);

        Assert.Null(buffer.ReadFront());
    }

    [Fact]
    public void Swap_PromotesBackBuffer_ToFrontBuffer()
    {
        var buffer = new FrameDoubleBuffer<FrameToken>();
        var first = new FrameToken("first");
        var second = new FrameToken("second");

        buffer.WriteBack(first);
        buffer.Swap();

        Assert.Same(first, buffer.ReadFront());

        buffer.WriteBack(second);
        buffer.Swap();

        Assert.Same(second, buffer.ReadFront());
    }

    public sealed record FrameToken(string Name);
}
