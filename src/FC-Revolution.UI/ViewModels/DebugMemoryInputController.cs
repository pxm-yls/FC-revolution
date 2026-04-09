using System.Globalization;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugJumpPageParseResult(
    int PageNumber,
    int PageIndex,
    string NormalizedInput);

internal static class DebugMemoryInputController
{
    public const string InvalidAddressMessage = "地址格式错误，请输入 4 位十六进制";
    public const string InvalidByteMessage = "数值格式错误，请输入 2 位十六进制";
    public const string InvalidJumpPageMessage = "跳页格式错误，请输入页码";

    public static bool TryParseAddress(string text, out ushort address) =>
        ushort.TryParse(
            NormalizeHexInput(text),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out address);

    public static bool TryParseByte(string text, out byte value) =>
        byte.TryParse(
            NormalizeHexInput(text),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);

    public static bool TryParseJumpPage(
        string text,
        int maxPage,
        out DebugJumpPageParseResult result)
    {
        result = default;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNumber))
            return false;

        pageNumber = DebugPageStateController.ClampPageNumber(pageNumber, maxPage);
        result = new DebugJumpPageParseResult(
            pageNumber,
            pageNumber - 1,
            pageNumber.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static string NormalizeHexInput(string text) =>
        (text ?? string.Empty).Trim().Replace("$", string.Empty);
}
