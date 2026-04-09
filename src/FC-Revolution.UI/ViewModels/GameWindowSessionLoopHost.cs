using System;
using System.Diagnostics;
using System.Threading;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowSessionLoopHost : IDisposable
{
    private readonly string _threadName;
    private readonly Func<bool> _shouldPauseFrameExecution;
    private readonly Func<CoreStepResult> _runFrame;
    private readonly Action<int> _reportFrameTimeMicros;
    private readonly Action<int> _reportFps;
    private readonly Action<Exception> _handleFailure;

    private readonly object _syncRoot = new();
    private Thread? _thread;
    private volatile bool _isRunning;

    public GameWindowSessionLoopHost(
        string threadName,
        Func<bool> shouldPauseFrameExecution,
        Func<CoreStepResult> runFrame,
        Action<int> reportFrameTimeMicros,
        Action<int> reportFps,
        Action<Exception> handleFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadName);
        ArgumentNullException.ThrowIfNull(shouldPauseFrameExecution);
        ArgumentNullException.ThrowIfNull(runFrame);
        ArgumentNullException.ThrowIfNull(reportFrameTimeMicros);
        ArgumentNullException.ThrowIfNull(reportFps);
        ArgumentNullException.ThrowIfNull(handleFailure);

        _threadName = threadName;
        _shouldPauseFrameExecution = shouldPauseFrameExecution;
        _runFrame = runFrame;
        _reportFrameTimeMicros = reportFrameTimeMicros;
        _reportFps = reportFps;
        _handleFailure = handleFailure;
    }

    public bool IsRunning => _isRunning;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = _threadName
            };
            _thread.Start();
        }
    }

    public void Stop(int joinMilliseconds = 300)
    {
        Thread? threadToJoin;
        lock (_syncRoot)
        {
            _isRunning = false;
            threadToJoin = _thread;
            _thread = null;
        }

        if (threadToJoin != null && !ReferenceEquals(Thread.CurrentThread, threadToJoin))
            threadToJoin.Join(joinMilliseconds);
    }

    public void Dispose() => Stop();

    private void RunLoop()
    {
        var frameTimer = Stopwatch.StartNew();
        var elapsedTimer = Stopwatch.StartNew();
        const double frameMs = 1000.0 / 60.0;
        var frameCount = 0;
        var fpsTimer = Stopwatch.StartNew();
        var fpsBucket = 0;

        while (_isRunning)
        {
            if (_shouldPauseFrameExecution())
            {
                Thread.Sleep(8);
                continue;
            }

            var targetMs = ++frameCount * frameMs;

            try
            {
                frameTimer.Restart();
                var stepResult = _runFrame();
                if (!stepResult.Success)
                    throw new InvalidOperationException(stepResult.ErrorMessage ?? "Core frame execution failed.");

                _reportFrameTimeMicros((int)(frameTimer.Elapsed.TotalMilliseconds * 1000));

                fpsBucket++;
                if (fpsTimer.Elapsed.TotalSeconds >= 0.5)
                {
                    _reportFps((int)(fpsBucket / fpsTimer.Elapsed.TotalSeconds));
                    fpsBucket = 0;
                    fpsTimer.Restart();
                }
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _handleFailure(ex);
                return;
            }

            var sleepMs = targetMs - elapsedTimer.Elapsed.TotalMilliseconds;
            if (sleepMs > 1.5)
                Thread.Sleep((int)sleepMs);
            else if (sleepMs < -200)
                frameCount = (int)(elapsedTimer.Elapsed.TotalMilliseconds / frameMs);
        }
    }
}
