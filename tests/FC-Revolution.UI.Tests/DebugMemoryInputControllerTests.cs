using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugMemoryInputControllerTests
{
    [Theory]
    [InlineData("0085", 0x0085)]
    [InlineData("$00AF", 0x00AF)]
    [InlineData("  $1f2b  ", 0x1F2B)]
    public void TryParseAddress_ParsesHexAddressInputs(string input, ushort expected)
    {
        var parsed = DebugMemoryInputController.TryParseAddress(input, out var address);

        Assert.True(parsed);
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GGGG")]
    [InlineData("12345")]
    public void TryParseAddress_ReturnsFalseForInvalidInputs(string input)
    {
        var parsed = DebugMemoryInputController.TryParseAddress(input, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("7F", 0x7F)]
    [InlineData("$ff", 0xFF)]
    [InlineData(" 0a ", 0x0A)]
    public void TryParseByte_ParsesHexByteInputs(string input, byte expected)
    {
        var parsed = DebugMemoryInputController.TryParseByte(input, out var value);

        Assert.True(parsed);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("100")]
    [InlineData("GG")]
    public void TryParseByte_ReturnsFalseForInvalidInputs(string input)
    {
        var parsed = DebugMemoryInputController.TryParseByte(input, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseJumpPage_ReturnsFalseForInvalidFormat()
    {
        var parsed = DebugMemoryInputController.TryParseJumpPage("page-2", maxPage: 8, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(-3, 1, 0, "1")]
    [InlineData(0, 1, 0, "1")]
    [InlineData(5, 5, 4, "5")]
    [InlineData(999, 8, 7, "8")]
    public void TryParseJumpPage_ClampsAndNormalizesPageNumber(
        int input,
        int expectedPageNumber,
        int expectedPageIndex,
        string expectedNormalizedInput)
    {
        var parsed = DebugMemoryInputController.TryParseJumpPage(
            input.ToString(),
            maxPage: 8,
            out var result);

        Assert.True(parsed);
        Assert.Equal(expectedPageNumber, result.PageNumber);
        Assert.Equal(expectedPageIndex, result.PageIndex);
        Assert.Equal(expectedNormalizedInput, result.NormalizedInput);
    }
}
