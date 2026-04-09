using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FC_Revolution.UI.Infrastructure;

internal static class StartupDiagnostics
{
    private const int MaxBufferedLines = 200;
    private static readonly object SyncRoot = new();
    private static readonly Queue<string> RecentLines = new();
    private static Stopwatch _sessionWatch = Stopwatch.StartNew();
    private static bool _enabled;
    private static bool _initialized;
    private static string _logPath = ResolveLogPath();

    public static event Action? Updated;

    public static string LogPath
    {
        get
        {
            lock (SyncRoot)
                return _enabled ? _logPath : string.Empty;
        }
    }

    public static void InitializeForCurrentProcess(string[] args)
    {
        lock (SyncRoot)
        {
            _enabled = string.Equals(Environment.GetEnvironmentVariable("FC_REVOLUTION_STARTUP_DEBUG"), "1", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Environment.GetEnvironmentVariable("FC_REVOLUTION_STARTUP_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);
            _logPath = ResolveLogPath();
            _sessionWatch = Stopwatch.StartNew();
            _initialized = true;
            RecentLines.Clear();

            if (!_enabled)
                return;

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

        Write("program", "startup diagnostics initialized");
        Write("program", $"log path = {LogPath}");
        Write("program", $"args = {(args.Length == 0 ? "<none>" : string.Join(" ", args))}");
        Write("program", $"process = {Environment.ProcessPath ?? "<unknown>"}");
        Write("program", $"base directory = {AppContext.BaseDirectory}");
        Write("program", $"current directory = {Environment.CurrentDirectory}");
        Write("program", $"os = {Environment.OSVersion}");
    }

    public static void Write(string stage, string message)
    {
        if (!_enabled)
            return;

        var line = BuildLine(stage, message);
        AppendLine(line);
    }

    public static void WriteException(string stage, string message, Exception exception)
    {
        if (!_enabled)
            return;

        var line = BuildLine(stage, $"{message}: {exception}");
        AppendLine(line);
    }

    public static string ReadRecentLog(int maxLines = 80)
    {
        if (!_enabled)
            return string.Empty;

        lock (SyncRoot)
        {
            return string.Join(
                Environment.NewLine,
                RecentLines.Skip(Math.Max(0, RecentLines.Count - Math.Max(1, maxLines))));
        }
    }

    private static string BuildLine(string stage, string message)
    {
        EnsureInitialized();
        return $"[{DateTime.Now:HH:mm:ss.fff} +{_sessionWatch.ElapsedMilliseconds,6}ms T{Environment.CurrentManagedThreadId:D2} {stage}] {message}";
    }

    private static void AppendLine(string line)
    {
        Action? updatedHandlers;
        lock (SyncRoot)
        {
            RecentLines.Enqueue(line);
            while (RecentLines.Count > MaxBufferedLines)
                RecentLines.Dequeue();

            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }

            updatedHandlers = Updated;
        }

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

        if (updatedHandlers == null)
            return;

        foreach (Action handler in updatedHandlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch
            {
            }
        }
    }

    private static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            _enabled = string.Equals(Environment.GetEnvironmentVariable("FC_REVOLUTION_STARTUP_DEBUG"), "1", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Environment.GetEnvironmentVariable("FC_REVOLUTION_STARTUP_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);
            _logPath = ResolveLogPath();
            _sessionWatch = Stopwatch.StartNew();
            _initialized = true;
        }
    }

    private static string ResolveLogPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FC_REVOLUTION_STARTUP_LOG");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.Combine(Path.GetTempPath(), "fc-revolution-startup.log");
    }
}
