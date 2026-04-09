namespace FCRevolution.Rendering.Common;

public sealed class FrameDoubleBuffer<T> where T : class
{
    private readonly object _lock = new();
    private T? _front;
    private T? _back;

    public void WriteBack(T frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_lock)
        {
            _back = frame;
        }
    }

    public void Swap()
    {
        lock (_lock)
        {
            (_front, _back) = (_back, _front);
        }
    }

    public T? ReadFront()
    {
        lock (_lock)
        {
            return _front;
        }
    }
}
