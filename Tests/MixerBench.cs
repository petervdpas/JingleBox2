using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker.Synth;

namespace JingleBox2.Tests;

/// <summary>
/// A way out whose mixer can be read back, standing in for the one that opens a sound card.
/// </summary>
internal sealed class MixerBench : ITrackerOutput
{
    /// <inheritdoc/>
    public int SampleRate => 44100;

    /// <inheritdoc/>
    public TrackMixer Mixer { get; } = new(44100);

    /// <inheritdoc/>
    public bool HasMixer => true;

    /// <inheritdoc/>
    public bool IsRunning => true;

    /// <inheritdoc/>
    public int Handle => 0;

    /// <inheritdoc/>
    public float Level => 0f;

    /// <inheritdoc/>
    public int RenderAheadMilliseconds => 0;

    /// <inheritdoc/>
    public int LatencyMilliseconds => 0;

    /// <inheritdoc/>
    public long Underruns => 0;

    /// <inheritdoc/>
    public void UseSampleRate(int rate) { }

    /// <inheritdoc/>
    public void UseRenderAhead(int milliseconds) { }

    /// <inheritdoc/>
    public void UseSizes(JingleBox2.Audio.Records.AudioSizes sizes) { }

    /// <inheritdoc/>
    public void EnsureStarted(IAudioEngine audio) { }

    /// <inheritdoc/>
    public void Restart(IAudioEngine audio) { }

    /// <inheritdoc/>
    public void Silence() { }

    /// <inheritdoc/>
    public void Dispose() { }
}

/// <summary>
/// A plugin instrument that fills its bus with one number, whatever it is asked to play.
/// </summary>
/// <remarks>
/// It fills rather than adds, which is what a plugin instrument does, and it plays whether or
/// not a note was sent: a plugin's own voices are its business and a host cannot tell whether
/// one is ringing. That is the whole reason this rule exists.
/// </remarks>
internal sealed class SteadyInstrument(float at) : IPluginInstrument
{
    /// <inheritdoc/>
    public PluginInfo Info { get; } = new("steady", "Steady", "", "", "");

    /// <inheritdoc/>
    public void NoteOn(int semitone, float velocity) { }

    /// <inheritdoc/>
    public void NoteOff(int semitone) { }

    /// <inheritdoc/>
    public void AllNotesOff() { }

    /// <inheritdoc/>
    public event Action<uint, double>? Edited { add { } remove { } }

    /// <inheritdoc/>
    public event Action? Reloaded { add { } remove { } }

    /// <inheritdoc/>
    public IReadOnlyList<PluginParameter> Parameters() => Array.Empty<PluginParameter>();

    /// <inheritdoc/>
    public double ValueOf(uint id) => 0;

    /// <inheritdoc/>
    public string TextFor(uint id, double value) => "";

    /// <inheritdoc/>
    public void SetValue(uint id, double value) { }

    /// <inheritdoc/>
    public byte[] SaveState() => Array.Empty<byte>();

    /// <inheritdoc/>
    public void LoadState(byte[]? state) { }

    /// <inheritdoc/>
    /// <summary>Plays no notes of its own, like every plugin but a drum machine or an arpeggiator.</summary>
    public int Played(Span<JingleBox2.Audio.Plugins.Records.PlayedNote> into) => 0;

    /// <inheritdoc/>
    public void Render(float[] buffer, int frames)
    {
        for (int sample = 0; sample < frames * 2; sample++) buffer[sample] = at;
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
