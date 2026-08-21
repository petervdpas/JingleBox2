using JingleBox2.Audio;
using JingleBox2.Tracker.Synth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JingleBox2.Tracker;

/// <summary>
/// Every recording an instrument plays, decoded once and kept. Handing the same data to any
/// number of voices is what gives a sample instrument its polyphony: a voice owns a position
/// in the file, never the file itself.
/// </summary>
/// <remarks>
/// Reading is bounded on purpose. An instrument is a jingle or a hit, not an album side, and
/// a voice reads from memory on the audio thread, so a file long enough to matter is refused
/// and reported rather than quietly turning the app into a disk cache.
/// </remarks>
public sealed class SampleStore
{
    /// <summary>Roughly fifty megabytes of stereo audio. Past this it is not an instrument.</summary>
    public const int MaxSeconds = 300;

    private readonly Dictionary<string, SampleData> _samples = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>Paths that could not be used, so a broken instrument is reported once.</summary>
    public IReadOnlyCollection<string> FailedPaths
    {
        get { lock (_lock) return _failed.ToArray(); }
    }

    /// <summary>Reads every instrument's file up front, so the first note is not late.</summary>
    public void Preload(IEnumerable<TrackerInstrument> instruments)
    {
        foreach (var instrument in instruments)
        {
            if (!instrument.IsSynth) Load(instrument.FilePath);
        }
    }

    /// <summary>The decoded recording, or null when there is nothing usable at that path.</summary>
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

    /// <summary>Forgets a file so an edited or re-recorded one is picked up next time.</summary>
    public void Invalidate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        lock (_lock)
        {
            _samples.Remove(filePath);
            _failed.Remove(filePath);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _samples.Clear();
            _failed.Clear();
        }
    }

    private static SampleData? Read(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            var info = WavFile.ReadInfo(filePath);
            if (info.SampleRate <= 0 || info.FrameCount <= 0) return null;
            if (info.FrameCount / (double)info.SampleRate > MaxSeconds) return null;

            var (samples, read) = WavFile.Read(filePath);
            return new SampleData(samples, read.Channels, read.SampleRate);
        }
        catch (Exception)
        {
            // Not a WAV, half written, or gone since the instrument was made: all the same
            // answer, which the caller reports as an instrument that will not sound.
            return null;
        }
    }
}
