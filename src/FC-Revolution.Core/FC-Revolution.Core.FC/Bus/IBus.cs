namespace FCRevolution.Core.Bus;

public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte data);
}
