using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Mute and solo over a track whose instrument is a plugin.
/// </summary>
/// <remarks>
/// **A plugin holds its own voices, so the only thing this side can turn down is the bus it
/// fills.** Our own voices are reachable one at a time by track and column, so a strip pressed
/// while they are sounding finds them; a plugin's level is one number written when a note is
/// sent, and until <see cref="TrackerPlayer.ApplyMix"/> put it back through the strip, a solo
/// pressed over a ringing plugin did nothing whatever until the next note. From a chair that is
/// a solo that does not work, which is exactly how it was reported.
///
/// Measured off what leaves the mixer rather than by reading what it was told, since the level
/// is the only thing that decides whether anybody hears the track. The plugin is a double that
/// fills its bus with a steady number, which is what a plugin instrument does and is the whole
/// of what this rule is about: it is rendered every block whether or not a note is sounding, so
/// nothing here has to load a plugin or press a key.
/// </remarks>
public class SoloReachesPluginsTests
{
    /// <summary>A song of two tracks, which is the fewest that can have a solo in it.</summary>
    private static Song Two()
    {
        var song = new Song { TrackCount = 2 };
        song.Normalize();

        return song;
    }

    /// <summary>
    /// The loudest sample of a block rendered with a plugin instrument on track 0.
    /// </summary>
    /// <param name="press">What is pressed on the mix before the block is rendered.</param>
    private static float Loudest(Action<Song> press)
    {
        var bench = new Bench();
        using var player = new TrackerPlayer(new QuietAudio(), output: bench);

        var song = Two();

        bench.Mixer.SetInstrument(0, new Steady(0.5f));

        player.Use(song);

        press(song);

        player.ApplyMix();

        var buffer = new float[441 * 2];
        bench.Mixer.Render(buffer, 441);

        float loudest = 0;
        foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));

        return loudest;
    }

    /// <summary>With nothing pressed, the plugin's track is heard.</summary>
    /// <remarks>
    /// Here so that the two silences below say something: a test that only ever asserts silence
    /// passes just as happily over a mixer that was never sounding at all.
    /// </remarks>
    [Fact]
    public void A_plugin_track_is_heard_with_nothing_pressed()
    {
        Assert.True(Loudest(_ => { }) > 0.1f);
    }

    /// <summary>Soloing another track silences the plugin that is already sounding.</summary>
    [Fact]
    public void Soloing_another_track_silences_a_ringing_plugin()
    {
        Assert.Equal(0f, Loudest(song => song.Mix[1].Solo = true));
    }

    /// <summary>Soloing the plugin's own track leaves it exactly where it was.</summary>
    [Fact]
    public void Soloing_its_own_track_leaves_the_plugin_alone()
    {
        Assert.Equal(Loudest(_ => { }), Loudest(song => song.Mix[0].Solo = true), 6);
    }

    /// <summary>Muting its own track silences it the same way.</summary>
    [Fact]
    public void Muting_its_own_track_silences_a_ringing_plugin()
    {
        Assert.Equal(0f, Loudest(song => song.Mix[0].Mute = true));
    }

    /// <summary>And the fader reaches it, which is the same write by another name.</summary>
    [Fact]
    public void The_fader_reaches_a_ringing_plugin()
    {
        float whole = Loudest(_ => { });
        float half = Loudest(song => song.Mix[0].Volume = 0.5);

        Assert.Equal(whole / 2, half, 3);
    }

    /// <summary>Solo taken off again puts it back.</summary>
    [Fact]
    public void Letting_a_solo_go_puts_the_plugin_back()
    {
        var bench = new Bench();
        using var player = new TrackerPlayer(new QuietAudio(), output: bench);

        var song = Two();

        bench.Mixer.SetInstrument(0, new Steady(0.5f));
        player.Use(song);

        song.Mix[1].Solo = true;
        player.ApplyMix();

        song.Mix[1].Solo = false;
        player.ApplyMix();

        var buffer = new float[441 * 2];
        bench.Mixer.Render(buffer, 441);

        Assert.Contains(buffer, sample => sample != 0f);
    }

    /// <summary>
    /// A way out whose mixer can be read back, standing in for the one that opens a sound card.
    /// </summary>
    private sealed class Bench : ITrackerOutput
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
    private sealed class Steady(float at) : IPluginInstrument
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
        public void Render(float[] buffer, int frames)
        {
            for (int sample = 0; sample < frames * 2; sample++) buffer[sample] = at;
        }

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
