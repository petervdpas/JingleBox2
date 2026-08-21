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

    /// <summary>
    /// The level a single voice comes out at. High enough to sit next to a sample played at
    /// its own level; several voices at once are held in by the saturation below rather than
    /// by leaving headroom nobody ever uses.
    /// </summary>
    public const float MasterGain = 0.9f;

    private readonly List<IVoice> _voices = new();
    private readonly object _lock = new();

    private IVoice[] _snapshot = Array.Empty<IVoice>();
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

    /// <summary>
    /// Sounds a recording on a track, under the same rules: the track's last note is cut, and
    /// the voice takes its place. The caller brings the audio, so the mixer never reads a file.
    /// </summary>
    public void NoteOn(int track, TrackerInstrument instrument, SampleData sample, Note note, float gain, float pan)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            track, gain, pan, SampleRate);

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

    /// <summary>A recording sounded once, for auditioning while editing.</summary>
    public void Preview(TrackerInstrument instrument, SampleData sample, Note note, float gain, double holdSeconds)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            SynthVoice.NoTrack, gain, 0f, SampleRate);

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

        IVoice[] playing;
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
            buffer[i] = SoftClip(buffer[i] * MasterGain);

        Reap();
    }

    /// <summary>Below this the bus is a wire; above it, it bends. Roughly -3 dB.</summary>
    public const float Knee = 0.7f;

    /// <summary>
    /// Saturates rather than clipping. A chord of voices can sum past full scale, and a hard
    /// clip on that sounds like a fault; this bends instead.
    /// </summary>
    /// <remarks>
    /// Bending starts at the knee rather than at zero. Recordings come through here too now,
    /// and a curve applied from the bottom up would quietly reshape every sample in the song
    /// on its way out, which is not the bus's business. Only what is loud enough to be a
    /// problem is touched.
    /// </remarks>
    public static float SoftClip(float value)
    {
        float magnitude = MathF.Abs(value);
        if (magnitude <= Knee) return value;

        float over = (magnitude - Knee) / (1 - Knee);
        float shaped = Knee + (1 - Knee) * MathF.Tanh(over);

        return value < 0 ? -shaped : shaped;
    }

    /// <summary>
    /// How loud a track is sounding, for a meter. Taken from the voices rather than from the
    /// mixed buffer: the voices are already summed together by the time that exists.
    /// </summary>
    public (float Left, float Right) LevelFor(int track)
    {
        if (track < 0) return (0, 0);

        float left = 0;
        float right = 0;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track != track || voice.IsFinished) continue;

                float level = voice.Level * MasterGain;
                float pan = voice.Pan;

                left = Math.Max(left, level * (pan <= 0 ? 1f : 1f - pan));
                right = Math.Max(right, level * (pan >= 0 ? 1f : 1f + pan));
            }
        }

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
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

    private void Add(IVoice voice)
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
