using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.AppServices;

internal sealed class SessionStreamBroadcaster
{
    private const int DefaultAudioChunkSize = 882;

    private readonly Guid _sessionId;
    private readonly IEmulatorCoreSession _coreSession;
    private readonly Action<Guid, SessionStreamBroadcaster> _onEmpty;
    private readonly object _gate = new();
    private readonly Dictionary<long, Channel<VideoFramePacket>> _videoSubscribers = new();
    private readonly Dictionary<long, AudioSubscriber> _audioSubscribers = new();

    private Action<VideoFramePacket>? _videoHandler;
    private Action<AudioPacket>? _audioHandler;
    private VideoFramePacket? _latestVideoFrame;
    private long _nextSubscriberId;

    public SessionStreamBroadcaster(Guid sessionId, IEmulatorCoreSession coreSession, Action<Guid, SessionStreamBroadcaster> onEmpty)
    {
        _sessionId = sessionId;
        _coreSession = coreSession;
        _onEmpty = onEmpty;
    }

    public BackendStreamSubscription Subscribe(int audioChunkSize)
    {
        var videoChannel = Channel.CreateBounded<VideoFramePacket>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
        var audioChannel = Channel.CreateBounded<AudioPacket>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
        AudioSubscriber audioSubscriber = audioChunkSize == DefaultAudioChunkSize
            ? new PassthroughAudioSubscriber(audioChannel)
            : new RechunkingAudioSubscriber(audioChannel, audioChunkSize);

        Action<VideoFramePacket>? attachVideo = null;
        Action<AudioPacket>? attachAudio = null;
        VideoFramePacket? latestVideoFrame;
        long subscriberId;

        lock (_gate)
        {
            subscriberId = ++_nextSubscriberId;
            _videoSubscribers[subscriberId] = videoChannel;
            _audioSubscribers[subscriberId] = audioSubscriber;
            latestVideoFrame = _latestVideoFrame;

            if (_videoHandler == null)
            {
                _videoHandler = OnVideoFrame;
                attachVideo = _videoHandler;
            }

            if (_audioHandler == null)
            {
                _audioHandler = OnAudioChunk;
                attachAudio = _audioHandler;
            }
        }

        if (attachVideo != null)
            _coreSession.VideoFrameReady += attachVideo;
        if (attachAudio != null)
            _coreSession.AudioReady += attachAudio;
        if (latestVideoFrame != null)
            videoChannel.Writer.TryWrite(latestVideoFrame);

        var disposed = 0;
        return new BackendStreamSubscription(
            videoChannel.Reader,
            audioChannel.Reader,
            () =>
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return ValueTask.CompletedTask;

                return UnsubscribeAsync(subscriberId);
            });
    }

    private void OnVideoFrame(VideoFramePacket packet)
    {
        lock (_gate)
        {
            if (_videoSubscribers.Count == 0)
                return;
        }

        Channel<VideoFramePacket>[] targets;
        lock (_gate)
        {
            if (_videoSubscribers.Count == 0)
                return;

            _latestVideoFrame = packet;
            targets = _videoSubscribers.Values.ToArray();
        }

        foreach (var channel in targets)
            channel.Writer.TryWrite(packet);
    }

    private void OnAudioChunk(AudioPacket packet)
    {
        AudioSubscriber[] targets;
        lock (_gate)
        {
            if (_audioSubscribers.Count == 0)
                return;

            targets = _audioSubscribers.Values.ToArray();
        }

        foreach (var subscriber in targets)
            subscriber.Publish(packet);
    }

    private ValueTask UnsubscribeAsync(long subscriberId)
    {
        Channel<VideoFramePacket>? videoChannel = null;
        AudioSubscriber? audioSubscriber = null;
        Action<VideoFramePacket>? detachVideo = null;
        Action<AudioPacket>? detachAudio = null;
        var becameEmpty = false;

        lock (_gate)
        {
            if (_videoSubscribers.Remove(subscriberId, out videoChannel) &&
                _videoSubscribers.Count == 0 &&
                _videoHandler != null)
            {
                detachVideo = _videoHandler;
                _videoHandler = null;
                _latestVideoFrame = null;
            }

            if (_audioSubscribers.Remove(subscriberId, out audioSubscriber) &&
                _audioSubscribers.Count == 0 &&
                _audioHandler != null)
            {
                detachAudio = _audioHandler;
                _audioHandler = null;
            }

            becameEmpty = _videoSubscribers.Count == 0 && _audioSubscribers.Count == 0;
        }

        videoChannel?.Writer.TryComplete();
        audioSubscriber?.Complete();

        if (detachVideo != null)
            _coreSession.VideoFrameReady -= detachVideo;
        if (detachAudio != null)
            _coreSession.AudioReady -= detachAudio;
        if (becameEmpty)
            _onEmpty(_sessionId, this);

        return ValueTask.CompletedTask;
    }

    private abstract class AudioSubscriber
    {
        private readonly Channel<AudioPacket> _channel;

        protected AudioSubscriber(Channel<AudioPacket> channel)
        {
            _channel = channel;
        }

        protected Channel<AudioPacket> Channel => _channel;

        public abstract void Publish(AudioPacket packet);

        public void Complete() => _channel.Writer.TryComplete();
    }

    private sealed class PassthroughAudioSubscriber : AudioSubscriber
    {
        public PassthroughAudioSubscriber(Channel<AudioPacket> channel)
            : base(channel)
        {
        }

        public override void Publish(AudioPacket packet)
        {
            Channel.Writer.TryWrite(packet);
        }
    }

    private sealed class RechunkingAudioSubscriber : AudioSubscriber
    {
        private readonly int _chunkSize;
        private readonly float[] _pending;
        private readonly object _gate = new();
        private int _pendingCount;

        public RechunkingAudioSubscriber(Channel<AudioPacket> channel, int chunkSize)
            : base(channel)
        {
            _chunkSize = Math.Max(1, chunkSize);
            _pending = new float[_chunkSize];
        }

        public override void Publish(AudioPacket packet)
        {
            lock (_gate)
            {
                var offset = 0;
                var samples = packet.Samples;
                while (offset < samples.Length)
                {
                    var toCopy = Math.Min(_chunkSize - _pendingCount, samples.Length - offset);
                    Array.Copy(samples, offset, _pending, _pendingCount, toCopy);
                    _pendingCount += toCopy;
                    offset += toCopy;

                    if (_pendingCount < _chunkSize)
                        continue;

                    var chunk = new float[_chunkSize];
                    Array.Copy(_pending, chunk, _chunkSize);
                    _pendingCount = 0;
                    Channel.Writer.TryWrite(new AudioPacket
                    {
                        Samples = chunk,
                        SampleRate = packet.SampleRate,
                        Channels = packet.Channels,
                        SampleFormat = packet.SampleFormat,
                        SampleCount = chunk.Length,
                        TimestampSeconds = packet.TimestampSeconds
                    });
                }
            }
        }
    }
}
