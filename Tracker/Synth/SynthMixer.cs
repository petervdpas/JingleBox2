using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Every sounding synth voice, summed into one buffer. One voice per track, the tracker way:
/// a new note cuts the one still ringing there. Auditions sit outside that and simply pile up.
/// </summary>
/// <remarks>
/// Rendering happens on the audio callback thread while notes are started from the clock and
/// from the UI, so the voice list is behind a lock. The critical sections are a few list
/// operations long; the sample loops themselves run on a snapshot.
/// </remarks>
public sealed class SynthMixer
{
    /// <summary>Past this, the oldest voice is taken rather than growing the mix forever.</summary>
    public const int MaxVoices = 48;

    /// <summary>Room for several voices before the sum reaches full scale.</summary>
    public const float MasterGain = 0.3f;

    private readonly List<SynthVoice> _voices = new();
    private readonly object _lock = new();

    private SynthVoice[] _snapshot = Array.Empty<SynthVoice>();
    private bool _snapshotStale = true;
    private int _noiseSeed;

    public SynthMixer(int sampleRate) => SampleRate = sampleRate;

    public int SampleRate { get; }

    public int VoiceCount
    {
        get { lock (_lock) return _voices.Count; }
    }

    public void NoteOn(int track, SynthPatch patch, Note note, float gain, float pan)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, track, gain, pan, SampleRate, NextSeed());

        lock (_lock)
        {
            if (track >= 0)
            {
                foreach (var playing in _voices)
                {
                    if (playing.Track == track) playing.Cut();
                }
            }

            Add(voice);
        }
    }

    /// <summary>Sounds a note that releases on its own, for auditioning while editing.</summary>
    public void Preview(SynthPatch patch, Note note, float gain, double holdSeconds)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, SynthVoice.NoTrack, gain, 0f, SampleRate, NextSeed());
        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
    }

    public void NoteOff(int track)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track == track) voice.NoteOff();
            }
        }
    }

    /// <summary>Follows the volume and pan columns while a note holds.</summary>
    public void SetLevels(int track, float gain, float? pan)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track != track) continue;

                voice.Gain = gain;
                if (pan.HasValue) voice.Pan = pan.Value;
            }
        }
    }

    /// <summary>Silence, now. Used by the transport rather than by a note off.</summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var voice in _voices) voice.Kill();

            _voices.Clear();
            _snapshotStale = true;
        }
    }

    /// <summary>
    /// Fills an interleaved stereo buffer with everything playing. Always writes the whole
    /// buffer: the audio callback has no way to say "nothing this time".
    /// </summary>
    public void Render(float[] buffer, int frames)
    {
        int samples = frames * 2;
        Array.Clear(buffer, 0, Math.Min(samples, buffer.Length));

        SynthVoice[] playing;
        lock (_lock)
        {
            if (_voices.Count == 0) return;

            if (_snapshotStale)
            {
                _snapshot = _voices.ToArray();
                _snapshotStale = false;
            }

            playing = _snapshot;
        }

        foreach (var voice in playing)
            voice.Render(buffer, frames);

        for (int i = 0; i < samples; i++)
            buffer[i] = Math.Clamp(buffer[i] * MasterGain, -1f, 1f);

        Reap();
    }

    /// <summary>Drops finished voices. Called after rendering, off the note-on path.</summary>
    private void Reap()
    {
        lock (_lock)
        {
            int removed = _voices.RemoveAll(v => v.IsFinished);
            if (removed > 0) _snapshotStale = true;
        }
    }

    private void Add(SynthVoice voice)
    {
        // Oldest first, so voice stealing takes the one that has been ringing longest.
        while (_voices.Count >= MaxVoices)
            _voices.RemoveAt(0);

        _voices.Add(voice);
        _snapshotStale = true;
    }

    /// <summary>A different seed per voice, so two noise hits are not the same noise.</summary>
    private int NextSeed() => System.Threading.Interlocked.Increment(ref _noiseSeed);
}
