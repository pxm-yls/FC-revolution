namespace FC_Revolution.UI.Tests;

internal static class FallbackInputTestData
{
    public const string ActionA = "a";
    public const string ActionB = "b";
    public const string ActionSelect = "select";
    public const string ActionStart = "start";
    public const string ActionUp = "up";
    public const string ActionDown = "down";
    public const string ActionLeft = "left";
    public const string ActionRight = "right";

    public const byte MaskA = 0x01;
    public const byte MaskB = 0x02;
    public const byte MaskSelect = 0x04;
    public const byte MaskStart = 0x08;
    public const byte MaskUp = 0x10;
    public const byte MaskDown = 0x20;
    public const byte MaskLeft = 0x40;
    public const byte MaskRight = 0x80;

    public static List<string> ActionIds(params string[] actionIds) => [.. actionIds];
}
