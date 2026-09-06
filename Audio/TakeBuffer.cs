using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// One lock over the whole of it, taken by the capture callback and by whoever starts and stops
/// a take. It is held for a copy of a list and nothing else, so the callback is never waiting on
/// anything that could take a moment.
/// </remarks>
public sealed class TakeBuffer : ITakeBuffer
{
    /// <summary>Everything heard, or the last moment of it while nothing is being recorded.</summary>
    private readonly List<byte> _heard = new();

    /// <summary>The one lock, and it guards <see cref="_recording"/> as well as the audio.</summary>
    /// <remarks>
    /// The flag and the audio are one fact rather than two, which is the whole point: read
    /// separately, a block can arrive after the flag says the take is over and while the audio
    /// is still in the buffer, and the trim then throws the take away.
    /// </remarks>
    private readonly object _lock = new();

    /// <summary>Backs <see cref="Recording"/>.</summary>
    private bool _recording;

    /// <summary>Backs <see cref="Take"/>.</summary>
    private byte[] _take = Array.Empty<byte>();

    /// <summary>
    /// How much is kept while nothing is being recorded: a fifth of a second at 44100, stereo,
    /// two bytes to a sample.
    /// </summary>
    /// <remarks>
    /// A length in bytes rather than in time, deliberately, since what a meter wants is the last
    /// moment and being exact about how long that is buys nothing. A capture at another rate
    /// simply keeps a little more or a little less of it.
    /// </remarks>
    public const int MonitorBytes = 44100 / 5 * 4;

    /// <inheritdoc/>
    public bool Recording { get { lock (_lock) return _recording; } }

    /// <inheritdoc/>
    public byte[] Take { get { lock (_lock) return _take; } }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _recording = false;
            _heard.Clear();
            _take = Array.Empty<byte>();
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lock)
        {
            _recording = true;
            _heard.Clear();
            _take = Array.Empty<byte>();
        }
    }

    /// <inheritdoc/>
    public byte[] Stop()
    {
        lock (_lock)
        {
            if (!_recording) return Array.Empty<byte>();

            _recording = false;
            _take = _heard.ToArray();

            return _take;
        }
    }

    /// <inheritdoc/>
    public void Add(byte[] block)
    {
        if (block == null || block.Length == 0) return;

        lock (_lock)
        {
            _heard.AddRange(block);

            if (!_recording && _heard.Count > MonitorBytes)
                _heard.RemoveRange(0, _heard.Count - MonitorBytes);
        }
    }

    /// <inheritdoc/>
    public byte[] Recent(int maxBytes, int bytesPerFrame)
    {
        if (maxBytes < 1 || bytesPerFrame < 1) return Array.Empty<byte>();

        lock (_lock)
        {
            if (_heard.Count == 0) return Array.Empty<byte>();

            int count = Math.Min(maxBytes, _heard.Count);
            count -= count % bytesPerFrame;

            if (count < 1) return Array.Empty<byte>();

            return _heard.GetRange(_heard.Count - count, count).ToArray();
        }
    }
}
