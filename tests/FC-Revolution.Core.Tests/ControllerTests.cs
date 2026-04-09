using FCRevolution.Core.Input;

namespace FC_Revolution.Core.Tests;

public class ControllerTests
{
    private static StandardController MakeController()
    {
        var c = new StandardController();
        c.Write(1); // strobe high
        c.Write(0); // strobe low → latch
        return c;
    }

    [Fact]
    public void NoButtonsPressed_AllReads_ReturnZero()
    {
        var c = MakeController();
        for (int i = 0; i < 8; i++)
            Assert.Equal(0, c.ReadState() & 0x01);
    }

    [Fact]
    public void ButtonA_ReturnsOnFirstPoll()
    {
        var c = new StandardController();
        c.SetButton(NesButton.A, true);
        c.Write(1); c.Write(0); // latch

        Assert.Equal(1, c.ReadState() & 0x01); // read 1: A
        Assert.Equal(0, c.ReadState() & 0x01); // read 2: B (not pressed)
    }

    [Fact]
    public void ButtonB_ReturnsOnSecondPoll()
    {
        var c = new StandardController();
        c.SetButton(NesButton.B, true);
        c.Write(1); c.Write(0);

        Assert.Equal(0, c.ReadState() & 0x01); // read 1: A (not pressed)
        Assert.Equal(1, c.ReadState() & 0x01); // read 2: B
    }

    [Fact]
    public void ButtonOrder_MatchesNesProtocol()
    {
        // NES poll order: A, B, Select, Start, Up, Down, Left, Right
        var c = new StandardController();
        c.SetButton(NesButton.A,      true);
        c.SetButton(NesButton.Select, true);
        c.SetButton(NesButton.Up,     true);
        c.SetButton(NesButton.Right,  true);
        c.Write(1); c.Write(0);

        bool[] expected = { true, false, true, false, true, false, false, true };
        for (int i = 0; i < 8; i++)
            Assert.Equal(expected[i], (c.ReadState() & 0x01) == 1);
    }

    [Fact]
    public void AfterEightPolls_FillsOnes()
    {
        var c = MakeController();
        for (int i = 0; i < 8; i++) c.ReadState();
        // Polls 9+ should return 1 (open-bus fill)
        Assert.Equal(1, c.ReadState() & 0x01);
    }

    [Fact]
    public void StrobeHigh_ContinuouslyReturnsA()
    {
        var c = new StandardController();
        c.SetButton(NesButton.A, true);
        c.Write(1); // keep strobe high

        // While strobe is held, every read returns current A state
        Assert.Equal(1, c.ReadState() & 0x01);
        Assert.Equal(1, c.ReadState() & 0x01);

        c.SetButton(NesButton.A, false);
        Assert.Equal(0, c.ReadState() & 0x01);
    }

    [Fact]
    public void Relatch_AfterPressChange_ReflectsNewState()
    {
        var c = new StandardController();
        c.SetButton(NesButton.A, true);
        c.Write(1); c.Write(0); // latch with A pressed

        // Read A (=1), exhaust remaining 7 reads
        Assert.Equal(1, c.ReadState() & 0x01);
        for (int i = 0; i < 7; i++) c.ReadState();

        // Change state and relatch
        c.SetButton(NesButton.A, false);
        c.SetButton(NesButton.B, true);
        c.Write(1); c.Write(0);

        Assert.Equal(0, c.ReadState() & 0x01); // A=0
        Assert.Equal(1, c.ReadState() & 0x01); // B=1
    }
}
