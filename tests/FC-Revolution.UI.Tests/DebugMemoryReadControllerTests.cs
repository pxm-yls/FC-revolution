using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugMemoryReadControllerTests
{
    [Fact]
    public void BuildReadSuccessState_ProjectsReadResultToViewState()
    {
        var state = DebugMemoryReadController.BuildReadSuccessState(0x0085, 0x2A, DebugViewModel.MemoryPageSize);

        Assert.Equal("2A", state.ValueInput);
        Assert.Equal(1, state.MemoryPageIndex);
        Assert.Equal("2", state.MemoryPageInput);
        Assert.Equal((ushort)0x0085, state.HighlightedAddress);
        Assert.Equal("已查询 $0085 = $2A", state.EditStatus);
    }

    [Fact]
    public void BuildReadSuccessState_ClampsMemoryPageSizeToAvoidInvalidDivision()
    {
        var state = DebugMemoryReadController.BuildReadSuccessState(0x000A, 0x7F, memoryPageSize: 0);

        Assert.Equal(10, state.MemoryPageIndex);
        Assert.Equal("11", state.MemoryPageInput);
    }
}
