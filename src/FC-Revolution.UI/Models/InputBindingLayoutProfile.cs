using System;

namespace FC_Revolution.UI.Models;

public sealed class InputBindingLayoutProfile
{
    public double BridgeX { get; set; } = 127.51171875;
    public double BridgeY { get; set; } = 53.74609375;
    public double LeftCircleX { get; set; } = 20.13671875;
    public double LeftCircleY { get; set; } = 36.46484375;
    public double DPadHorizontalX { get; set; } = 30.5234375;
    public double DPadHorizontalY { get; set; } = 64.4296875;
    public double DPadVerticalX { get; set; } = 52.16015625;
    public double DPadVerticalY { get; set; } = 48.953125;
    public double RightCircleX { get; set; } = 250.8046875;
    public double RightCircleY { get; set; } = 32.90234375;
    public double BDecorationX { get; set; } = 263.55859375;
    public double BDecorationY { get; set; } = 74.0703125;
    public double ADecorationX { get; set; } = 295.953125;
    public double ADecorationY { get; set; } = 52.6640625;
    public double SelectDecorationX { get; set; } = 146.97265625;
    public double SelectDecorationY { get; set; } = 98.5234375;
    public double StartDecorationX { get; set; } = 195.4140625;
    public double StartDecorationY { get; set; } = 98.90625;

    public InputBindingLayoutSlot Up { get; set; } = new(65.49609375, 58.0078125, 22, 22);
    public InputBindingLayoutSlot Down { get; set; } = new(66.140625, 103.890625, 22, 22);
    public InputBindingLayoutSlot Left { get; set; } = new(39.69140625, 77.62890625, 22, 22);
    public InputBindingLayoutSlot Right { get; set; } = new(88.08203125, 79.48828125, 22, 22);
    public InputBindingLayoutSlot Select { get; set; } = new(155.9375, 107.9453125, 17, 8);
    public InputBindingLayoutSlot Start { get; set; } = new(204.47265625, 108.1796875, 17, 8);
    public InputBindingLayoutSlot B { get; set; } = new(277.55859375, 89.203125, 24, 22);
    public InputBindingLayoutSlot A { get; set; } = new(309.27734375, 66.18359375, 24, 22);

    public static InputBindingLayoutProfile CreateDefault() => new();

    public InputBindingLayoutProfile Clone() => new()
    {
        BridgeX = BridgeX,
        BridgeY = BridgeY,
        LeftCircleX = LeftCircleX,
        LeftCircleY = LeftCircleY,
        DPadHorizontalX = DPadHorizontalX,
        DPadHorizontalY = DPadHorizontalY,
        DPadVerticalX = DPadVerticalX,
        DPadVerticalY = DPadVerticalY,
        RightCircleX = RightCircleX,
        RightCircleY = RightCircleY,
        BDecorationX = BDecorationX,
        BDecorationY = BDecorationY,
        ADecorationX = ADecorationX,
        ADecorationY = ADecorationY,
        SelectDecorationX = SelectDecorationX,
        SelectDecorationY = SelectDecorationY,
        StartDecorationX = StartDecorationX,
        StartDecorationY = StartDecorationY,
        Up = Up.Clone(),
        Down = Down.Clone(),
        Left = Left.Clone(),
        Right = Right.Clone(),
        Select = Select.Clone(),
        Start = Start.Clone(),
        B = B.Clone(),
        A = A.Clone()
    };

    public void ResetToDefaults()
    {
        var defaults = CreateDefault();
        Up = defaults.Up;
        Down = defaults.Down;
        Left = defaults.Left;
        Right = defaults.Right;
        Select = defaults.Select;
        Start = defaults.Start;
        B = defaults.B;
        A = defaults.A;
    }

    public InputBindingLayoutSlot GetSlot(string actionId) => actionId.Trim().ToLowerInvariant() switch
    {
        "up" => Up,
        "down" => Down,
        "left" => Left,
        "right" => Right,
        "select" => Select,
        "start" => Start,
        "b" => B,
        "a" => A,
        _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, null)
    };

    public void Sanitize()
    {
        BridgeX = Math.Clamp(BridgeX, 0, 320);
        BridgeY = Math.Clamp(BridgeY, 0, 126);
        LeftCircleX = Math.Clamp(LeftCircleX, 0, 269);
        LeftCircleY = Math.Clamp(LeftCircleY, 0, 63);
        DPadHorizontalX = Math.Clamp(DPadHorizontalX, 0, 290);
        DPadHorizontalY = Math.Clamp(DPadHorizontalY, 0, 123);
        DPadVerticalX = Math.Clamp(DPadVerticalX, 0, 329);
        DPadVerticalY = Math.Clamp(DPadVerticalY, 0, 84);
        RightCircleX = Math.Clamp(RightCircleX, 0, 269);
        RightCircleY = Math.Clamp(RightCircleY, 0, 63);
        BDecorationX = Math.Clamp(BDecorationX, 0, 329);
        BDecorationY = Math.Clamp(BDecorationY, 0, 123);
        ADecorationX = Math.Clamp(ADecorationX, 0, 329);
        ADecorationY = Math.Clamp(ADecorationY, 0, 123);
        SelectDecorationX = Math.Clamp(SelectDecorationX, 0, 338);
        SelectDecorationY = Math.Clamp(SelectDecorationY, 0, 132);
        StartDecorationX = Math.Clamp(StartDecorationX, 0, 338);
        StartDecorationY = Math.Clamp(StartDecorationY, 0, 132);
        Up.Sanitize();
        Down.Sanitize();
        Left.Sanitize();
        Right.Sanitize();
        Select.Sanitize();
        Start.Sanitize();
        A.Sanitize();
        B.Sanitize();
    }
}

public sealed class InputBindingLayoutSlot(double centerX, double centerY, double width, double height)
{
    public double CenterX { get; set; } = centerX;
    public double CenterY { get; set; } = centerY;
    public double Width { get; set; } = width;
    public double Height { get; set; } = height;

    public InputBindingLayoutSlot Clone() => new(CenterX, CenterY, Width, Height);

    public void Sanitize()
    {
        CenterX = Math.Clamp(CenterX, 0, 356);
        CenterY = Math.Clamp(CenterY, 0, 150);
        Width = Math.Clamp(Width, 8, 48);
        Height = Math.Clamp(Height, 8, 36);
    }
}
