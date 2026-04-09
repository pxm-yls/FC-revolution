namespace FCRevolution.Core;

public interface IEmulationComponent
{
    void Reset();
    void Clock();
}

public interface IStateSerializable
{
    byte[] SerializeState();
    void DeserializeState(byte[] state);
}
