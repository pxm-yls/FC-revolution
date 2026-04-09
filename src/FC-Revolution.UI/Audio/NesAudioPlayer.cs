using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace FC_Revolution.UI.Audio;

/// <summary>
/// Streams NES APU float samples to the audio device via OpenAL (macOS built-in framework).
/// Gracefully disables itself if OpenAL is unavailable.
/// </summary>
internal sealed class NesAudioPlayer : IDisposable
{
    // ── OpenAL P/Invoke ───────────────────────────────────────────────────
    private const string AlLib = "/System/Library/Frameworks/OpenAL.framework/OpenAL";

    [DllImport(AlLib)] private static extern IntPtr alcOpenDevice(string? deviceName);
    [DllImport(AlLib)] private static extern IntPtr alcCreateContext(IntPtr device, IntPtr attr);
    [DllImport(AlLib)] private static extern bool   alcMakeContextCurrent(IntPtr ctx);
    [DllImport(AlLib)] private static extern void   alcDestroyContext(IntPtr ctx);
    [DllImport(AlLib)] private static extern bool   alcCloseDevice(IntPtr device);
    [DllImport(AlLib)] private static extern void   alGenSources(int n, out uint source);
    [DllImport(AlLib)] private static extern void   alGenBuffers(int n, [Out] uint[] bufs);
    [DllImport(AlLib)] private static extern unsafe void alBufferData(uint buf, int fmt, void* data, int size, int freq);
    [DllImport(AlLib)] private static extern void   alSourceQueueBuffers(uint src, int n, uint[] bufs);
    [DllImport(AlLib)] private static extern void   alSourceUnqueueBuffers(uint src, int n, [Out] uint[] bufs);
    [DllImport(AlLib)] private static extern void   alSourcePlay(uint src);
    [DllImport(AlLib)] private static extern void   alGetSourcei(uint src, int param, out int val);
    [DllImport(AlLib)] private static extern void   alDeleteSources(int n, ref uint src);
    [DllImport(AlLib)] private static extern void   alDeleteBuffers(int n, uint[] bufs);

    private const int AlFormatMono16     = 0x1101;
    private const int AlBuffersProcessed = 0x1016;
    private const int AlSourceState      = 0x1010;
    private const int AlPlaying          = 0x1012;

    // ── Audio configuration ───────────────────────────────────────────────
    // APU outputs 1789773/40 = 44744 samples/sec; use same rate to avoid drift / resampling clicks
    private const int SampleRate       = 44744;
    private const int NumAlBuffers     = 4;
    private const int SamplesPerBuffer = 746;              // 44744 / 60 Hz ≈ one frame
    private const int MaxQueueSize     = SampleRate * 2;  // 2-second safety cap

    // ── OpenAL state ─────────────────────────────────────────────────────
    private IntPtr _device;
    private IntPtr _context;
    private uint   _source;
    private uint[] _alBuffers = Array.Empty<uint>();
    private bool   _buffersGenerated;

    // ── Sample queue (emu thread → audio thread) ─────────────────────────
    private readonly ConcurrentQueue<float[]> _queue = new();
    private Thread?  _thread;
    private volatile bool _alive;
    private int _queuedSamples;
    private float[]? _activeChunk;
    private int _activeChunkIndex;

    public bool IsAvailable { get; private set; }
    public string? InitError   { get; private set; }
    public long SamplesReceived => _samplesReceived;
    private long _samplesReceived;

    /// <summary>Output gain multiplier. Default 2.0 – NES APU output is typically 0.05–0.3 range.</summary>
    public float Volume { get; set; } = 2.0f;

    public NesAudioPlayer()
    {
        try   { Init(); }
        catch (Exception ex) { InitError = ex.GetType().Name + ": " + ex.Message; Cleanup(); }
    }

    private unsafe void Init()
    {
        _device = alcOpenDevice(null);
        if (_device == IntPtr.Zero)
        {
            InitError = "OpenAL device unavailable";
            return;
        }

        _context = alcCreateContext(_device, IntPtr.Zero);
        if (_context == IntPtr.Zero)
        {
            InitError = "OpenAL context unavailable";
            return;
        }
        alcMakeContextCurrent(_context);

        alGenSources(1, out _source);
        _alBuffers = new uint[NumAlBuffers];
        alGenBuffers(NumAlBuffers, _alBuffers);
        _buffersGenerated = true;

        // Prime buffers with silence so playback starts immediately
        var silence = stackalloc short[SamplesPerBuffer];
        foreach (var buf in _alBuffers)
            alBufferData(buf, AlFormatMono16, silence, SamplesPerBuffer * 2, SampleRate);

        alSourceQueueBuffers(_source, NumAlBuffers, _alBuffers);
        alSourcePlay(_source);

        IsAvailable = true;
        _alive = true;
        _thread = new Thread(StreamLoop)
        {
            IsBackground = true,
            Name = "NesAudio",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    /// <summary>Called from the emulation thread whenever a new audio chunk is ready.</summary>
    public void PushChunk(float[] chunk)
    {
        Interlocked.Add(ref _samplesReceived, chunk.Length);
        if (!IsAvailable) return;
        if (chunk.Length == 0)
            return;

        var queuedSamples = Volatile.Read(ref _queuedSamples);
        if (queuedSamples >= MaxQueueSize)
            return;

        _queue.Enqueue(chunk);
        Interlocked.Add(ref _queuedSamples, chunk.Length);
    }

    private unsafe void StreamLoop()
    {
        // OpenAL context must be made current on the thread that issues AL calls
        alcMakeContextCurrent(_context);

        var pcm      = new short[SamplesPerBuffer];
        var unqueued = new uint[1];
        var requeue  = new uint[1];

        // One-pole high-pass filter state — removes DC offset from APU signal
        // alpha = 0.9978 ≈ 15 Hz cutoff at 44744 Hz (matches real NES analog HP filter)
        const float HpAlpha = 0.9978f;
        float hpPrev = 0f, hpOut = 0f;
        float lastSample = 0f;   // held when queue is empty — avoids step-discontinuity clicks

        fixed (short* p = pcm)
        {
            while (_alive)
            {
                alGetSourcei(_source, AlBuffersProcessed, out int processed);

                for (int b = 0; b < processed; b++)
                {
                    alSourceUnqueueBuffers(_source, 1, unqueued);

                    float vol = Volume;
                    for (int i = 0; i < SamplesPerBuffer; i++)
                    {
                        // Sample-hold: keep last value when queue is empty (no sudden silence jump)
                        if (TryDequeueSample(out var s)) lastSample = s;
                        // High-pass filter: y[n] = alpha*(y[n-1] + x[n] - x[n-1])
                        hpOut  = HpAlpha * (hpOut + lastSample - hpPrev);
                        hpPrev = lastSample;
                        // Soft-clip via tanh: smooth saturation, no hard-clip transients
                        p[i] = (short)(MathF.Tanh(hpOut * vol) * 32767f);
                    }

                    alBufferData(unqueued[0], AlFormatMono16, p, SamplesPerBuffer * 2, SampleRate);
                    requeue[0] = unqueued[0];
                    alSourceQueueBuffers(_source, 1, requeue);
                }

                // Restart if source stalled (should be rare with always-requeue strategy)
                alGetSourcei(_source, AlSourceState, out int state);
                if (state != AlPlaying)
                    alSourcePlay(_source);

                Thread.Sleep(1);
            }
        }
    }

    public void Dispose()
    {
        _alive = false;
        _thread?.Join(300);
        Cleanup();
    }

    private void Cleanup()
    {
        if (_source != 0)
        {
            try { uint s = _source; alDeleteSources(1, ref s); } catch { }
            _source = 0;
        }
        if (_buffersGenerated)
        {
            try { alDeleteBuffers(_alBuffers.Length, _alBuffers); } catch { }
            _buffersGenerated = false;
        }
        if (_context != IntPtr.Zero)
        {
            try { alcMakeContextCurrent(IntPtr.Zero); alcDestroyContext(_context); } catch { }
            _context = IntPtr.Zero;
        }
        if (_device != IntPtr.Zero)
        {
            try { alcCloseDevice(_device); } catch { }
            _device = IntPtr.Zero;
        }
        IsAvailable = false;
    }

    private bool TryDequeueSample(out float sample)
    {
        while (true)
        {
            if (_activeChunk != null && _activeChunkIndex < _activeChunk.Length)
            {
                sample = _activeChunk[_activeChunkIndex++];
                if (_activeChunkIndex >= _activeChunk.Length)
                {
                    _activeChunk = null;
                    _activeChunkIndex = 0;
                }

                return true;
            }

            if (!_queue.TryDequeue(out var chunk))
            {
                sample = 0f;
                return false;
            }

            Interlocked.Add(ref _queuedSamples, -chunk.Length);
            _activeChunk = chunk;
            _activeChunkIndex = 0;
        }
    }
}
