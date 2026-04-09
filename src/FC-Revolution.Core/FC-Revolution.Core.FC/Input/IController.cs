namespace FCRevolution.Core.Input;

[Flags]
public enum NesButton : byte
{
    None   = 0x00,
    A      = 0x01,
    B      = 0x02,
    Select = 0x04,
    Start  = 0x08,
    Up     = 0x10,
    Down   = 0x20,
    Left   = 0x40,
    Right  = 0x80,
}

public interface IController
{
    byte ReadState();
    void Write(byte data);
    void SetButton(NesButton button, bool pressed);
}
