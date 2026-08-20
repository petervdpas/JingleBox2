using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedBass;

namespace JingleBox2.Tracker;

/// <summary>
/// Holds each instrument's sample in memory once and hands out a channel per note.
/// BASS samples give polyphony for free: several channels can play the same loaded sample
/// at different rates, which is exactly what a chord in a tracker is.
/// </summary>
public sealed class TrackerSampleBank : IDisposable
{
    /// <summary>How many notes one instrument can sound at the same time.</summary>
    public const int MaxPolyphonyPerInstrument = 16;

    /// <summary>Fallback when BASS will not report a sample's rate.</summary>
    public const int DefaultSampleRate = 44100;

    private readonly Dictionary<string, LoadedSample> _samples = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private bool _disposed;

    private sealed record LoadedSample(int Handle, int SampleRate);

    /// <summary>Paths that failed to load, so a broken instrument is reported once, not per note.</summary>
    public IReadOnlyCollection<string> FailedPaths
    {
        get { lock (_lock) return _failed.ToArray(); }
    }

    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    /// <summary>Loads every instrument up front so the first note is not late.</summary>
    public void Preload(IEnumerable<TrackerInstrument> instruments)
    {
        foreach (var instrument in instruments)
        {
            if (!string.IsNullOrWhiteSpace(instrument.FilePath))
                Load(instrument.FilePath);
        }
    }

    /// <summary>
    /// A channel ready to play, already pitched for <paramref name="note"/>, or 0 when the
    /// instrument's file is missing or unreadable.
    /// </summary>
    public int GetChannel(TrackerInstrument instrument, Note note)
    {
        var sample = Load(instrument.FilePath);
        if (sample == null) return 0;

        int channel = Bass.SampleGetChannel(sample.Handle);
        if (channel == 0) return 0;

        double frequency = PitchRatio.FrequencyFor(note, instrument.BaseNote, sample.SampleRate);
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Frequency, (float)frequency);

        return channel;
    }

    private LoadedSample? Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        lock (_lock)
        {
            if (_disposed) return null;
            if (_samples.TryGetValue(filePath, out var cached)) return cached;
            if (_failed.Contains(filePath)) return null;

            if (!File.Exists(filePath))
            {
                _failed.Add(filePath);
                return null;
            }

            int handle = Bass.SampleLoad(filePath, 0, 0, MaxPolyphonyPerInstrument, BassFlags.Default);
            if (handle == 0)
            {
                _failed.Add(filePath);
                return null;
            }

            // The file's own rate is the reference every pitch shift is measured against.
            var info = new SampleInfo();
            int sampleRate = Bass.SampleGetInfo(handle, info) ? info.Frequency : DefaultSampleRate;

            var loaded = new LoadedSample(handle, sampleRate);
            _samples[filePath] = loaded;
            return loaded;
        }
    }

    /// <summary>Drops a single file, so re-recording an instrument's sample takes effect.</summary>
    public void Invalidate(string filePath)
    {
        lock (_lock)
        {
            if (_samples.Remove(filePath, out var sample))
                Bass.SampleFree(sample.Handle);

            _failed.Remove(filePath);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var sample in _samples.Values)
                Bass.SampleFree(sample.Handle);

            _samples.Clear();
            _failed.Clear();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Clear();
    }
}
