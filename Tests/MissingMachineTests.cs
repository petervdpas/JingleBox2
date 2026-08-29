using System;
using System.Collections.Generic;
using JingleBox2.Audio.Enums;
using JingleBox2.Config.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Machines;
using JingleBox2.Machines.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// An instrument whose machine is not installed makes no sound at all.
/// </summary>
/// <remarks>
/// The awkward part of this rule is that everything needed to play such an instrument is
/// present. Its settings travel inside the song and the engine is compiled into the
/// application, so it would play, and play something that sounds finished, on a machine the
/// song no longer has. That is what makes it worth refusing rather than a case that fails on
/// its own.
///
/// The engine handed in throws on every member, so these say more than that nothing was heard:
/// they say the audio was never asked for anything. A guard that returned nought after starting
/// a device would pass a test that only looked at the answer.
/// </remarks>
public class MissingMachineTests
{
    /// <summary>An audio engine that refuses every question, so touching it is a failure.</summary>
    private sealed class NoAudio : IAudioEngine
    {
        /// <summary>What every member says, since none of them should be reached.</summary>
        private static Exception Asked([System.Runtime.CompilerServices.CallerMemberName] string what = "") =>
            new InvalidOperationException($"the audio was asked for {what} and should not have been");

        /// <inheritdoc/>
        public int PadCount => throw Asked();
        /// <inheritdoc/>
        public float GetOutputLevel() => throw Asked();
        /// <inheritdoc/>
        public IEnumerable<OutputDevice> GetOutputDevices() => throw Asked();
        /// <inheritdoc/>
        public void SetOutputDevice(int deviceId) => throw Asked();
        /// <inheritdoc/>
        public void EnsureInitialized() => throw Asked();
        /// <inheritdoc/>
        public event EventHandler<PadPlaybackChanged>? PadPlaybackChanged { add { } remove { } }
        /// <inheritdoc/>
        public bool IsPadPlaying(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public double GetPadProgress(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public float GetPadLevel(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public float GetPadChannelVolume(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public void PlaySample(int padIndex, string filePath, float volume) => throw Asked();
        /// <inheritdoc/>
        public void PlayStream(int padIndex, string url, float volume) => throw Asked();
        /// <inheritdoc/>
        public void StopSample(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public void SetPadSource(int padIndex, PadSourceKind kind, string? source) => throw Asked();
        /// <inheritdoc/>
        public void SetPadVolume(int padIndex, float volume) => throw Asked();
        /// <inheritdoc/>
        public void SetPadLoop(int padIndex, bool loop) => throw Asked();
        /// <inheritdoc/>
        public void SetPadFadeIn(int padIndex, double seconds) => throw Asked();
        /// <inheritdoc/>
        public void SetPadFadeOut(int padIndex, double seconds) => throw Asked();
        /// <inheritdoc/>
        public void Resize(int newPadCount) => throw Asked();
        /// <inheritdoc/>
        public void SetPadInsert(int padIndex, IAudioInsert? insert) => throw Asked();
        /// <inheritdoc/>
        public IAudioInsert? GetPadInsert(int padIndex) => throw Asked();
        /// <inheritdoc/>
        public int PadSampleRate(int padIndex) => throw Asked();
        /// <inheritdoc/>
        /// <remarks>Nothing, since nothing was ever started. The only member that may be reached.</remarks>
        public void Dispose() { }
    }

    /// <summary>A projects list holding exactly the machines named, and nothing else.</summary>
    private static IMachineProjects Holding(params string[] ids)
    {
        var projects = new MachineProjects();

        projects.Keep(Array.ConvertAll(ids, id => new MachineProject { Id = id }));

        return projects;
    }

    private static TrackerInstrument On(TrackerInstrumentKind kind) =>
        new() { Id = Guid.NewGuid().ToString(), Name = "Kick", Kind = kind };

    /// <summary>With nothing installed, nothing sounds and the audio is never touched.</summary>
    [Fact]
    public void Nothing_installed_means_nothing_sounds()
    {
        using var player = new TrackerPlayer(new NoAudio(), Holding());

        foreach (var kind in new[]
                 {
                     TrackerInstrumentKind.Sampler, TrackerInstrumentKind.Kit,
                     TrackerInstrumentKind.Synth, TrackerInstrumentKind.MonoSynth,
                     TrackerInstrumentKind.Sample
                 })
        {
            Assert.Equal(0, player.Preview(On(kind), new Note(60)));
        }
    }

    /// <summary>The machine that is installed is the only one allowed through.</summary>
    /// <remarks>
    /// The one installed here is Zampler, so a sampler instrument gets past the guard and is
    /// refused by the audio instead, which is the proof that the guard is what stopped the
    /// others rather than something further down.
    /// </remarks>
    [Fact]
    public void Only_the_installed_machine_is_let_through()
    {
        using var player = new TrackerPlayer(new NoAudio(), Holding("machine.zampler"));

        Assert.Equal(0, player.Preview(On(TrackerInstrumentKind.Kit), new Note(60)));
        Assert.Equal(0, player.Preview(On(TrackerInstrumentKind.MonoSynth), new Note(60)));

        Assert.ThrowsAny<Exception>(
            () => player.Preview(On(TrackerInstrumentKind.Sampler), new Note(60)));
    }

    /// <summary>
    /// A plugin is never refused here, whatever is installed.
    /// </summary>
    /// <remarks>
    /// A plugin is not a machine project and has no slot to be missing from. What a plugin needs
    /// is the plugin itself, which is a different absence with an answer of its own, so this
    /// guard must not stand in front of it.
    /// </remarks>
    [Fact]
    public void A_plugin_is_never_refused_for_a_missing_machine()
    {
        using var player = new TrackerPlayer(new NoAudio(), Holding());

        Assert.ThrowsAny<Exception>(
            () => player.Preview(On(TrackerInstrumentKind.Plugin), new Note(60)));
    }

    /// <summary>A note that is not a note is refused before the machine is even asked about.</summary>
    [Fact]
    public void An_unplayable_note_is_refused_first()
    {
        using var player = new TrackerPlayer(new NoAudio(), Holding("machine.zampler"));

        Assert.Equal(0, player.Preview(On(TrackerInstrumentKind.Sampler), Note.Empty));
    }

    /// <summary>The projects list answers for each kind by the machine's own slot id.</summary>
    [Fact]
    public void The_projects_answer_per_kind()
    {
        var some = Holding("machine.zampler", "machine.bongabong");

        Assert.True(some.Has(TrackerInstrumentKind.Sampler));
        Assert.True(some.Has(TrackerInstrumentKind.Kit));

        Assert.False(some.Has(TrackerInstrumentKind.Synth));
        Assert.False(some.Has(TrackerInstrumentKind.MonoSynth));
        Assert.False(some.Has(TrackerInstrumentKind.Sample));
    }

    /// <summary>An id is matched however it is cased, since a folder name is what it came from.</summary>
    [Fact]
    public void The_slot_id_is_matched_whatever_its_case()
    {
        Assert.True(Holding("MACHINE.ZAMPLER").Has(TrackerInstrumentKind.Sampler));
    }

    /// <summary>
    /// A player told nothing about the machines answers that every one of them is missing.
    /// </summary>
    /// <remarks>
    /// A player built without being wired up has not been told what is installed, and silence is
    /// the honest answer to that rather than playing everything on the strength of not knowing.
    /// </remarks>
    [Fact]
    public void A_player_told_nothing_plays_nothing()
    {
        using var player = new TrackerPlayer(new NoAudio());

        Assert.Equal(0, player.Preview(On(TrackerInstrumentKind.Sampler), new Note(60)));
    }
}
