using System;
using System.Diagnostics;

namespace FC_Revolution.UI.Infrastructure;

internal static class RuntimeDiagnostics
{
    public static void Write(string category, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff} {category}] {message}";

        try
        {
            Console.WriteLine(line);
        }
        catch
        {
        }

        try
        {
            Trace.WriteLine(line);
        }
        catch
        {
        }
    }
}
