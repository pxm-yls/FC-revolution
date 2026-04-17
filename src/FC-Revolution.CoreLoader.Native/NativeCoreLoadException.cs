namespace FCRevolution.CoreLoader.Native;

public sealed class NativeCoreLoadException : InvalidOperationException
{
    public NativeCoreLoadException(string message)
        : base(message)
    {
    }
}
