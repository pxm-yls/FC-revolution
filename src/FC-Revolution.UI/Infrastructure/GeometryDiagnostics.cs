using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FC_Revolution.UI.Infrastructure;

internal static class GeometryDiagnostics
{
    private static readonly object SyncRoot = new();
    private static Stopwatch _sessionWatch = Stopwatch.StartNew();
    private static bool _initialized;
    private static string _logPath = ResolveLogPath();

    public static string LogPath
    {
        get
        {
            EnsureInitialized();
            lock (SyncRoot)
                return _logPath;
        }
    }

    public static void InitializeForCurrentProcess(string[] args)
    {
        lock (SyncRoot)
        {
            _logPath = ResolveLogPath();
            _sessionWatch = Stopwatch.StartNew();
            _initialized = true;

            try
            {
                var directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_logPath, string.Empty, Encoding.UTF8);
            }
            catch
            {
            }
        }

        Write("program", "geometry diagnostics initialized");
        Write("program", $"log path = {LogPath}");
        Write("program", $"args = {(args.Length == 0 ? "<none>" : string.Join(" ", args))}");
    }

    public static void Write(string stage, string message)
    {
        var line = BuildLine(stage, message);
        AppendLine(line);
    }

    public static void WriteException(string stage, string message, Exception exception)
    {
        var line = BuildLine(stage, $"{message}: {exception}");
        AppendLine(line);
    }

    private static string BuildLine(string stage, string message)
    {
        EnsureInitialized();
        return $"[{DateTime.Now:HH:mm:ss.fff} +{_sessionWatch.ElapsedMilliseconds,6}ms T{Environment.CurrentManagedThreadId:D2} {stage}] {message}";
    }

    private static void AppendLine(string line)
    {
        lock (SyncRoot)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        try
        {
            Trace.WriteLine(line);
        }
        catch
        {
        }
    }

    private static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            _logPath = ResolveLogPath();
            _sessionWatch = Stopwatch.StartNew();
            _initialized = true;

            try
            {
                var directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_logPath, string.Empty, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private static string ResolveLogPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FC_REVOLUTION_GEOMETRY_LOG");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.Combine(Path.GetTempPath(), "fc-revolution-geometry.log");
    }
}
