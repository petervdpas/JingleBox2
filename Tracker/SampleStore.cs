using JingleBox2.Audio;
using JingleBox2.Tracker.Synth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// A dictionary by path, with one lock over both it and the list of what failed, so the clock
/// thread and the drawing thread can ask at once. The decoding itself is done off the lock:
/// reading a file is the slow part and holding the lock across it would queue every other
/// track's first note behind whichever one asked first.
/// </remarks>
public sealed class SampleStore : ISampleStore
{
    /// <summary>Reading and writing WAV files. Holds nothing, so one serves the whole object.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>Roughly fifty megabytes of stereo audio. Past this it is not an instrument.</summary>
    public const int MaxSeconds = 300;

    /// <summary>What has been decoded, by the path it was asked for under.</summary>
    private readonly Dictionary<string, SampleData> _samples = new(StringComparer.Ordinal);

    /// <summary>
    /// What could not be decoded, so the same missing file is not reopened on every note.
    /// </summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    /// <summary>One lock over both, since a path moves between them.</summary>
    private readonly object _lock = new();

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FailedPaths
    {
        get { lock (_lock) return _failed.ToArray(); }
    }

    /// <inheritdoc/>
    public void Preload(IEnumerable<TrackerInstrument> instruments)
    {
        foreach (var instrument in instruments)
        {
            if (instrument.Kit != null)
            {
                foreach (string path in instrument.Kit.Files) Load(path);
                continue;
            }

            if (instrument.Zones != null)
            {
                foreach (string path in instrument.Zones.Files) Load(path);
                continue;
            }

            if (!instrument.IsSynth) Load(instrument.FilePath);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The lock is taken twice and not held across the read, so two threads asking for the same
    /// file at once both decode it and the second's copy replaces the first's. That costs one
    /// read and nothing else: the data is never written into, and a voice holds a position
    /// rather than the array.
    /// </remarks>
    public SampleData? Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        lock (_lock)
        {
            if (_samples.TryGetValue(filePath, out var cached)) return cached;
            if (_failed.Contains(filePath)) return null;
        }

        var data = Read(filePath);

        lock (_lock)
        {
            if (data == null)
            {
                _failed.Add(filePath);
                return null;
            }

            _samples[filePath] = data;
            return data;
        }
    }

    /// <inheritdoc/>
    public void Invalidate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        lock (_lock)
        {
            _samples.Remove(filePath);
            _failed.Remove(filePath);
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            _samples.Clear();
            _failed.Clear();
        }
    }

    /// <summary>
    /// Decodes one file, or answers null for every reason a file can be no good.
    /// </summary>
    /// <remarks>
    /// Not a WAV, half written, longer than <see cref="MaxSeconds"/>, or gone since the
    /// instrument was made: all the same answer, which the caller reports as an instrument that
    /// will not sound. The length is checked off the header before the audio is read, so a file
    /// too long to use is never held in memory even briefly.
    /// </remarks>
    private SampleData? Read(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            var info = _wav.ReadInfo(filePath);
            if (info.SampleRate <= 0 || info.FrameCount <= 0) return null;
            if (info.FrameCount / (double)info.SampleRate > MaxSeconds) return null;

            var (samples, read) = _wav.Read(filePath);
            return new SampleData(samples, read.Channels, read.SampleRate);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
