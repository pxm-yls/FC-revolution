using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace FC_Revolution.UI.Audio;

/// <summary>
/// Streams float samples to the audio device via OpenAL (macOS built-in framework).
/// Gracefully disables itself if OpenAL is unavailable.
/// </summary>
internal sealed class CoreAudioPlayer : IDisposable
{
    private const string AlLib = "/System/Library/Frameworks/OpenAL.framework/OpenAL";

    [DllImport(AlLib)] private static extern IntPtr alcOpenDevice(string? deviceName);
    [DllImport(AlLib)] private static extern IntPtr alcCreateContext(IntPtr device, IntPtr attr);
    [DllImport(AlLib)] private static extern bool alcMakeContextCurrent(IntPtr ctx);
    [DllImport(AlLib)] private static extern void alcDestroyContext(IntPtr ctx);
    [DllImport(AlLib)] private static extern bool alcCloseDevice(IntPtr device);
    [DllImport(AlLib)] private static extern void alGenSources(int n, out uint source);
    [DllImport(AlLib)] private static extern void alGenBuffers(int n, [Out] uint[] bufs);
    [DllImport(AlLib)] private static extern unsafe void alBufferData(uint buf, int fmt, void* data, int size, int freq);
    [DllImport(AlLib)] private static extern void alSourceQueueBuffers(uint src, int n, uint[] bufs);
    [DllImport(AlLib)] private static extern void alSourceUnqueueBuffers(uint src, int n, [Out] uint[] bufs);
    [DllImport(AlLib)] private static extern void alSourcePlay(uint src);
    [DllImport(AlLib)] private static extern void alGetSourcei(uint src, int param, out int val);
    [DllImport(AlLib)] private static extern void alDeleteSources(int n, ref uint src);
    [DllImport(AlLib)] private static extern void alDeleteBuffers(int n, uint[] bufs);

    private const int AlFormatMono16 = 0x1101;
    private const int AlBuffersProcessed = 0x1016;
    private const int AlSourceState = 0x1010;
    private const int AlPlaying = 0x1012;

    private const int SampleRate = 44744;
    private const int NumAlBuffers = 4;
    private const int SamplesPerBuffer = 746;
    private const int MaxQueueSize = SampleRate * 2;

    private IntPtr _device;
    private IntPtr _context;
    private uint _source;
    private uint[] _alBuffers = Array.Empty<uint>();
    private bool _buffersGenerated;

    private readonly ConcurrentQueue<float[]> _queue = new();
    private Thread? _thread;
    private volatile bool _alive;
    private int _queuedSamples;
    private float[]? _activeChunk;
    private int _activeChunkIndex;

    public bool IsAvailable { get; private set; }
    public string? InitError { get; private set; }
    public long SamplesReceived => _samplesReceived;
    private long _samplesReceived;

    public float Volume { get; set; } = 2.0f;

    public CoreAudioPlayer()
    {
        try { Init(); }
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
            Name = "CoreAudio",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public void PushChunk(float[] chunk)
    {
        Interlocked.Add(ref _samplesReceived, chunk.Length);
        if (!IsAvailable)
            return;

        if (chunk.Length == 0)
            return;

        while (_queuedSamples > MaxQueueSize && _queue.TryDequeue(out var dropped))
            _queuedSamples -= dropped.Length;

        _queue.Enqueue(chunk);
        _queuedSamples += chunk.Length;
    }

    private unsafe void StreamLoop()
    {
        var recycle = new uint[1];
        var pcm = new short[SamplesPerBuffer];
        while (_alive)
        {
            try
            {
                alGetSourcei(_source, AlBuffersProcessed, out var processed);
                while (processed-- > 0)
                {
                    alSourceUnqueueBuffers(_source, 1, recycle);
                    FillPcmBuffer(pcm);
                    fixed (short* p = pcm)
                        alBufferData(recycle[0], AlFormatMono16, p, pcm.Length * 2, SampleRate);
                    alSourceQueueBuffers(_source, 1, recycle);
                }

                alGetSourcei(_source, AlSourceState, out var state);
                if (state != AlPlaying)
                    alSourcePlay(_source);
            }
            catch
            {
                InitError ??= "Audio stream loop failed";
                Cleanup();
                return;
            }

            Thread.Sleep(2);
        }
    }

    private void FillPcmBuffer(short[] pcm)
    {
        Array.Clear(pcm, 0, pcm.Length);
        for (var i = 0; i < pcm.Length; i++)
        {
            if (!TryReadNextSample(out var sample))
                continue;

            var scaled = Math.Clamp(sample * Volume, -1f, 1f);
            pcm[i] = (short)Math.Round(scaled * short.MaxValue);
        }
    }

    private bool TryReadNextSample(out float sample)
    {
        sample = 0f;
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

            if (!_queue.TryDequeue(out _activeChunk))
                return false;

            _queuedSamples -= _activeChunk.Length;
            _activeChunkIndex = 0;
        }
    }

    public void Dispose()
    {
        _alive = false;
        if (_thread != null && _thread.IsAlive)
            _thread.Join(100);

        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        IsAvailable = false;

        try
        {
            if (_source != 0)
                alDeleteSources(1, ref _source);
        }
        catch
        {
        }

        try
        {
            if (_buffersGenerated && _alBuffers.Length > 0)
                alDeleteBuffers(_alBuffers.Length, _alBuffers);
        }
        catch
        {
        }

        try
        {
            if (_context != IntPtr.Zero)
                alcDestroyContext(_context);
        }
        catch
        {
        }

        try
        {
            if (_device != IntPtr.Zero)
                alcCloseDevice(_device);
        }
        catch
        {
        }

        _source = 0;
        _context = IntPtr.Zero;
        _device = IntPtr.Zero;
        _alBuffers = Array.Empty<uint>();
        _buffersGenerated = false;
    }
}
