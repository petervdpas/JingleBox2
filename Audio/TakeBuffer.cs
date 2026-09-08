using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// One lock over the whole of it, taken by whichever thread is handing blocks over and by whoever
/// starts and stops a take. It is held for a copy into an array and nothing else, so nobody is
/// ever waiting on anything that could take a moment.
///
/// **Chunks rather than one growing array, and that is about the thread rather than the memory.**
/// A list doubles by allocating the new size and copying, so late in a long take it allocates a
/// hundred megabytes and copies into it, in the middle of whoever handed the last block over. That
/// was tolerable while the only caller was the capture's own thread and stopped being so when the
/// recorder's bus started reading here: the mixing thread is the one this application is careful
/// to allocate nothing on. A take now costs one fixed block per <see cref="ChunkBytes"/> of audio
/// however long it runs, and no copy at all until it is asked for.
/// </remarks>
public sealed class TakeBuffer : ITakeBuffer
{
    /// <summary>Everything heard, in order, as blocks of one size.</summary>
    /// <remarks>
    /// Every block but the last is full, which is what lets the length be arithmetic rather than
    /// a walk. What the first one holds begins at <see cref="_first"/>, since the monitor's trim
    /// takes bytes off the front and taking them off exactly is what keeps the last moment the
    /// length it says it is.
    /// </remarks>
    private readonly List<byte[]> _chunks = new();

    /// <summary>Blocks that have been finished with, kept to be filled again.</summary>
    /// <remarks>
    /// Monitoring cycles the same two or three blocks for ever, so after the first moment it
    /// allocates nothing at all. Held to <see cref="MostSpare"/>, because the other end of this
    /// is a take of an hour: recycling all of those would be keeping the whole recording in the
    /// name of never allocating again.
    /// </remarks>
    private readonly Stack<byte[]> _spare = new();

    /// <summary>How big one block is.</summary>
    /// <remarks>
    /// Under the 85000 bytes that would put it on the large object heap, which is the whole
    /// reason for a number rather than a guess: blocks above that line are allocated somewhere
    /// that is only collected with a full pass, and this allocates them for as long as a take
    /// runs. At 48000 stereo it is about a sixth of a second apiece.
    /// </remarks>
    public const int ChunkBytes = 32768;

    /// <summary>How many finished blocks are kept to be filled again.</summary>
    private const int MostSpare = 4;

    /// <summary>Where what is held begins inside the first block.</summary>
    private int _first;

    /// <summary>How much of the last block is real.</summary>
    private int _filled;

    /// <summary>How much is held altogether.</summary>
    /// <remarks>
    /// Kept rather than worked out, since every add and every trim already knows what it moved
    /// and <see cref="Recent"/> would otherwise walk the whole take to find its end.
    /// </remarks>
    private long _held;

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

    /// <summary>The longest take that can be handed over as one array.</summary>
    /// <remarks>
    /// Which is what a byte array can hold, and is about three hours of stereo at 48000. A take
    /// that runs past it keeps its beginning rather than its end, since what somebody has been
    /// recording for three hours starts at the start; said here rather than left to be an
    /// exception on the thread that pressed stop.
    /// </remarks>
    private const int Most = int.MaxValue - 64;

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

            EmptyLocked();

            _take = Array.Empty<byte>();
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lock)
        {
            _recording = true;

            EmptyLocked();

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
            _take = ReadLocked(_held > Most ? Most : (int)_held, fromTheEnd: false);

            return _take;
        }
    }

    /// <inheritdoc/>
    public void Add(byte[] block) => Add(block, block?.Length ?? 0);

    /// <inheritdoc/>
    public void Add(byte[] block, int bytes)
    {
        if (block == null || bytes <= 0) return;

        bytes = Math.Min(bytes, block.Length);

        lock (_lock)
        {
            int at = 0;

            while (at < bytes)
            {
                if (_chunks.Count == 0 || _filled == ChunkBytes) OpenLocked();

                int room = Math.Min(ChunkBytes - _filled, bytes - at);

                Buffer.BlockCopy(block, at, _chunks[^1], _filled, room);

                _filled += room;
                _held += room;
                at += room;
            }

            if (!_recording) TrimLocked();
        }
    }

    /// <inheritdoc/>
    public byte[] Recent(int maxBytes, int bytesPerFrame)
    {
        if (maxBytes < 1 || bytesPerFrame < 1) return Array.Empty<byte>();

        lock (_lock)
        {
            int count = (int)Math.Min(maxBytes, _held);

            count -= count % bytesPerFrame;

            return count < 1 ? Array.Empty<byte>() : ReadLocked(count, fromTheEnd: true);
        }
    }

    /// <summary>Starts another block, taking one back off the shelf where there is one.</summary>
    private void OpenLocked()
    {
        _chunks.Add(_spare.Count > 0 ? _spare.Pop() : new byte[ChunkBytes]);
        _filled = 0;
    }

    /// <summary>Throws away everything held, keeping a few blocks to fill again.</summary>
    private void EmptyLocked()
    {
        foreach (byte[] chunk in _chunks)
            if (_spare.Count < MostSpare)
                _spare.Push(chunk);

        _chunks.Clear();

        _first = 0;
        _filled = 0;
        _held = 0;
    }

    /// <summary>How much of one block is real, which the first and the last both answer their own way.</summary>
    /// <param name="chunk">Which block, by its place.</param>
    private int WithinLocked(int chunk) =>
        (chunk == _chunks.Count - 1 ? _filled : ChunkBytes) - (chunk == 0 ? _first : 0);

    /// <summary>Drops what is past the monitor's own length, off the front.</summary>
    /// <remarks>
    /// Exactly rather than by whole blocks, so what is held is the length this class says it is.
    /// A block whose last byte has gone is put back on the shelf rather than let go, which is
    /// what makes an afternoon of monitoring free after its first moment.
    /// </remarks>
    private void TrimLocked()
    {
        while (_held > MonitorBytes && _chunks.Count > 0)
        {
            long over = _held - MonitorBytes;
            int within = WithinLocked(0);

            if (over < within)
            {
                _first += (int)over;
                _held -= over;

                return;
            }

            _held -= within;

            if (_spare.Count < MostSpare) _spare.Push(_chunks[0]);

            _chunks.RemoveAt(0);
            _first = 0;

            if (_chunks.Count == 0) _filled = 0;
        }
    }

    /// <summary>
    /// Copies what is held into one array, from its start or from its end.
    /// </summary>
    /// <remarks>
    /// The one place a take becomes a single block of memory, and it is on whichever thread asked
    /// rather than on the one handing audio over. From the end for the meter, which wants the last
    /// moment, and from the start for a take, which is the performance.
    /// </remarks>
    /// <param name="count">How many bytes to take, which the caller has already held to what is there.</param>
    /// <param name="fromTheEnd">Whether to take the last that many rather than the first.</param>
    private byte[] ReadLocked(int count, bool fromTheEnd)
    {
        if (count < 1) return Array.Empty<byte>();

        var made = new byte[count];

        if (fromTheEnd)
        {
            int need = count;

            for (int chunk = _chunks.Count - 1; chunk >= 0 && need > 0; chunk--)
            {
                int from = chunk == 0 ? _first : 0;
                int within = WithinLocked(chunk);
                int took = Math.Min(within, need);

                Buffer.BlockCopy(_chunks[chunk], from + within - took, made, need - took, took);

                need -= took;
            }

            return made;
        }

        int at = 0;

        for (int chunk = 0; chunk < _chunks.Count && at < count; chunk++)
        {
            int from = chunk == 0 ? _first : 0;
            int took = Math.Min(WithinLocked(chunk), count - at);

            Buffer.BlockCopy(_chunks[chunk], from, made, at, took);

            at += took;
        }

        return made;
    }
}
