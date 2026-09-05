using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What happens to a block of audio when something goes wrong on the way through the mixer.
/// </summary>
/// <remarks>
/// This is the one place in the application where a fault is fatal rather than annoying. It runs
/// on the sound card's own thread, inside a callback with a deadline, and an exception there is
/// not a message on the status bar: it is the process gone, mid-session, with whatever was not
/// saved. So the question this asks is never "does it sound right", it is "what does it do when
/// it is lied to", and almost every test here hands it something a caller should not.
///
/// It exists because of a real one. Everything the mixing uses is sized from the frame count it
/// was called with, and <c>EnsureBusses</c> reallocates the busses whenever that count changes,
/// so two threads rendering at once with different counts is one of them shortening the arrays
/// the other is halfway through. It took the application down after an afternoon's work with an
/// index outside the bounds of the array. The same crash arrives from the other direction as
/// well, from one thread with a buffer shorter than the frame count it claims, which the top of
/// the method guarded against and the three stages after it did not.
///
/// Two threads is not a hypothetical. The sound card's own thread renders in step and a thread
/// of its own renders ahead into a queue, never both, except while one is being swapped for the
/// other: <c>TrackerOutput.StopMixingAhead</c> waits two tenths of a second for the ahead thread
/// and then carries on regardless, rightly, since a plugin holding it up must not hang the
/// application. Changing the output device or the render-ahead setting is that moment.
/// </remarks>
public class MixerRenderTests
{
    /// <summary>The sample rate everything here is rendered at.</summary>
    private const int Rate = 44100;

    /// <summary>The ahead thread's fixed block, which is one of the two sizes that collided.</summary>
    private const int Ahead = 512;

    /// <summary>And a sound card's, deliberately neither a divisor nor a multiple of it.</summary>
    private const int Card = 200;

    /// <summary>As many tracks as a song can have, which is what everything here is indexed by.</summary>
    private static readonly int Tracks = Song.MaxTrackCount;

    /// <summary>A patch that certainly makes a noise: on at once and staying on.</summary>
    private static SynthPatch Loud() => new()
    {
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 5000
    };

    /// <summary>A mixer with a note held on one track, so every block has something in it.</summary>
    private static TrackMixer Sounding(int track = 0)
    {
        var mixer = new TrackMixer(Rate);
        mixer.NoteOn(track, 0, Loud(), new Note(60), 1f, 0f);
        return mixer;
    }

    /// <summary>Renders one block and hands back what came out.</summary>
    private static float[] Block(TrackMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    // ---- The crash itself -------------------------------------------------------------

    /// <summary>
    /// Two threads, two block sizes, one mixer. Without the guard this throws within a few
    /// hundred blocks, which is why it is run for thousands.
    /// </summary>
    [Fact]
    public async Task Two_threads_rendering_different_block_sizes_do_not_tear()
    {
        var mixer = Sounding();
        mixer.NoteOn(1, 0, Loud(), new Note(64), 1f, 0f);

        Exception? fell = null;

        void Run(int frames)
        {
            var buffer = new float[frames * 2];

            try
            {
                for (int block = 0; block < 3000; block++) mixer.Render(buffer, frames);
            }
            catch (Exception error)
            {
                Interlocked.CompareExchange(ref fell, error, null);
            }
        }

        await Task.WhenAll(Task.Run(() => Run(Ahead)), Task.Run(() => Run(Card)));

        Assert.Null(fell);
    }

    /// <summary>
    /// And with more threads than there will ever be, at sizes that keep forcing the busses to
    /// be made again. Two is the real case; this is the one that would find a hole in the guard.
    /// </summary>
    [Fact]
    public async Task Many_threads_at_many_sizes_do_not_tear()
    {
        var mixer = Sounding();

        Exception? fell = null;

        var sizes = new[] { 64, 512, 128, 1024, 200, 333 };

        var running = sizes.Select(frames => Task.Run(() =>
        {
            var buffer = new float[frames * 2];

            try
            {
                for (int block = 0; block < 1500; block++) mixer.Render(buffer, frames);
            }
            catch (Exception error)
            {
                Interlocked.CompareExchange(ref fell, error, null);
            }
        })).ToArray();

        await Task.WhenAll(running);

        Assert.Null(fell);
    }

    /// <summary>
    /// A second thread is given silence rather than a place in a queue, and is given it at once.
    /// One quiet block is a click; a blocked callback is every stream on the device stuttering,
    /// which is the rule the output already keeps for a queue that has run dry.
    /// </summary>
    [Fact]
    public async Task A_second_thread_is_given_silence_rather_than_kept_waiting()
    {
        var mixer = new TrackMixer(Rate);

        using var inside = new ManualResetEventSlim(false);
        using var carryOn = new ManualResetEventSlim(false);

        var slow = new SlowInstrument(inside, carryOn);
        mixer.SetInstrument(0, slow);

        var holder = Task.Run(() => mixer.Render(new float[Ahead * 2], Ahead));

        Assert.True(inside.Wait(5000), "the first thread never reached the instrument");

        var mine = new float[Card * 2];
        Array.Fill(mine, 0.5f);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        mixer.Render(mine, Card);
        clock.Stop();

        carryOn.Set();
        await holder;

        Assert.All(mine, sample => Assert.Equal(0f, sample));
        Assert.True(clock.ElapsedMilliseconds < 500, "it waited " + clock.ElapsedMilliseconds + " ms");
    }

    /// <summary>
    /// The state moving while the audio is being rendered, which is what a hand on the keyboard
    /// and a pattern playing both do. Notes, instruments, inserts and the track order all move
    /// under two rendering threads at once.
    /// </summary>
    [Fact]
    public async Task The_mix_may_be_changed_while_it_is_being_rendered()
    {
        var mixer = Sounding();

        Exception? fell = null;
        bool go = true;

        void Watch(Action work)
        {
            try { work(); }
            catch (Exception error) { Interlocked.CompareExchange(ref fell, error, null); }
        }

        var rendering = new[] { Ahead, Card }.Select(frames => Task.Run(() => Watch(() =>
        {
            var buffer = new float[frames * 2];
            while (Volatile.Read(ref go)) mixer.Render(buffer, frames);
        }))).ToArray();

        var churning = Task.Run(() => Watch(() =>
        {
            for (int round = 0; round < 400; round++)
            {
                int track = round % Tracks;

                mixer.NoteOn(track, round % Song.MaxNoteColumns, Loud(), new Note(48 + round % 24), 1f, 0f);
                mixer.SetInsert(track, round % 3 == 0 ? new Quiet() : null);
                mixer.SetInstrument(track, round % 5 == 0 ? new Silent() : null);
                mixer.SetLevels(track, 0, 0.5f, 0f);
                mixer.SetDucking(track, 0.5, (track + 1) % Tracks, 50);
                mixer.MoveTrack(track, (track + 1) % Tracks);
                mixer.NoteOff(track, round % Song.MaxNoteColumns);

                if (round % 50 == 0) mixer.StopAll();
            }
        }));

        await churning;
        Volatile.Write(ref go, false);
        await Task.WhenAll(rendering);

        Assert.Null(fell);
    }

    // ---- Blocks that are not what they say they are -----------------------------------

    /// <summary>A block of no frames is nothing to do rather than an exception.</summary>
    [Fact]
    public void No_frames_is_nothing_to_do()
    {
        var mixer = Sounding();
        var buffer = new float[64];
        Array.Fill(buffer, 0.25f);

        mixer.Render(buffer, 0);

        Assert.All(buffer, sample => Assert.Equal(0.25f, sample));
    }

    /// <summary>And a negative count, which no caller should send and one arithmetic slip will.</summary>
    [Fact]
    public void A_negative_frame_count_is_nothing_to_do()
    {
        var mixer = Sounding();
        var buffer = new float[64];

        mixer.Render(buffer, -1);
        mixer.Render(buffer, int.MinValue);

        Assert.All(buffer, sample => Assert.Equal(0f, sample));
    }

    /// <summary>
    /// A count so large that doubling it goes round the houses. It must not be read as a
    /// negative length, and it must not be trusted against the buffer either.
    /// </summary>
    [Fact]
    public void A_frame_count_that_overflows_is_held_to_the_buffer()
    {
        var mixer = Sounding();
        var buffer = new float[Card * 2];

        mixer.Render(buffer, int.MaxValue);

        Assert.Contains(buffer, sample => sample != 0f);
    }

    /// <summary>
    /// A buffer shorter than the frame count claims. Nothing is written past its end, and what
    /// room there is gets real audio: the caller has a sound card waiting either way.
    /// </summary>
    [Fact]
    public void A_buffer_shorter_than_the_block_is_filled_as_far_as_it_goes()
    {
        var mixer = Sounding();

        foreach (int length in new[] { 2, 3, 17, 64, Card * 2 - 1 })
        {
            var buffer = new float[length];

            mixer.Render(buffer, Ahead);

            Assert.Contains(buffer, sample => sample != 0f);
        }
    }

    /// <summary>And a buffer with no room at all, which is a caller that has already gone wrong.</summary>
    [Fact]
    public void A_buffer_with_no_room_is_nothing_to_do()
    {
        var mixer = Sounding();

        mixer.Render(Array.Empty<float>(), Ahead);
        mixer.Render(new float[1], Ahead);
    }

    /// <summary>
    /// A buffer longer than the block leaves the rest of itself alone, since the caller may be
    /// holding something after it.
    /// </summary>
    [Fact]
    public void A_longer_buffer_is_only_written_as_far_as_the_block()
    {
        var mixer = Sounding();

        var buffer = new float[Card * 4];
        Array.Fill(buffer, 0.75f);

        mixer.Render(buffer, Card);

        Assert.All(buffer.Skip(Card * 2), sample => Assert.Equal(0.75f, sample));
    }

    /// <summary>
    /// The block size changing from call to call, which is what a sound card really does and is
    /// what makes the busses be built again.
    /// </summary>
    [Fact]
    public void The_block_size_may_change_between_calls()
    {
        var mixer = Sounding();

        foreach (int frames in new[] { 512, 200, 1024, 64, 333, 1, 2048, 512 })
        {
            var buffer = new float[frames * 2];

            mixer.Render(buffer, frames);

            Assert.All(buffer, sample => Assert.True(float.IsFinite(sample)));
        }
    }

    // ---- Things in the chain that misbehave -------------------------------------------

    /// <summary>
    /// A plugin instrument that throws takes its own track down and nothing else. That is the
    /// whole promise of running them out of process, kept here for one that is in process.
    /// </summary>
    [Fact]
    public void An_instrument_that_throws_costs_only_its_own_track()
    {
        var mixer = Sounding(1);
        mixer.SetInstrument(0, new Angry());

        var buffer = Block(mixer, Card);

        Assert.Contains(buffer, sample => sample != 0f);
        Assert.Equal(0f, mixer.GetTrackLevel(0));
        Assert.True(mixer.GetTrackLevel(1) > 0);
    }

    /// <summary>And it goes on throwing, block after block, without ever taking the mix with it.</summary>
    [Fact]
    public void An_instrument_that_always_throws_never_stops_the_mix()
    {
        var mixer = Sounding(1);
        mixer.SetInstrument(0, new Angry());

        for (int block = 0; block < 200; block++)
        {
            var buffer = Block(mixer, Card);
            Assert.Contains(buffer, sample => sample != 0f);
        }
    }

    /// <summary>An insert that throws leaves the audio it was handed alone rather than the mix.</summary>
    [Fact]
    public void An_insert_that_throws_costs_only_its_own_track()
    {
        var mixer = Sounding(1);
        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);
        mixer.SetInsert(0, new Furious());

        var buffer = Block(mixer, Card);

        Assert.Contains(buffer, sample => sample != 0f);
    }

    /// <summary>And one on the master, where there is nothing after it to save the block.</summary>
    [Fact]
    public void A_master_insert_that_throws_still_leaves_a_block()
    {
        var mixer = Sounding();
        mixer.SetMasterInsert(new Furious());

        var buffer = Block(mixer, Card);

        Assert.All(buffer, sample => Assert.True(float.IsFinite(sample)));
    }

    /// <summary>
    /// A preview instrument that throws is the same promise for a note played by hand, and it
    /// leaves silence rather than whatever was in the scratch from the block before.
    /// </summary>
    [Fact]
    public void A_preview_instrument_that_throws_leaves_silence_behind_it()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetPreviewInstrument(new Angry());
        mixer.PreviewPlugin(new Note(60), 1f, 5, VoiceEnding.Cut);

        var buffer = Block(mixer, Card);

        Assert.All(buffer, sample => Assert.Equal(0f, sample));
    }

    /// <summary>
    /// An instrument that writes further than it was asked to must not reach past its own bus.
    /// The bus is as long as the block, so this is really asking whether the block is honest.
    /// </summary>
    [Fact]
    public void An_instrument_that_writes_past_its_block_cannot_reach_the_output()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetInstrument(0, new Greedy());

        var buffer = new float[Card * 4];
        Array.Fill(buffer, 0.75f);

        mixer.Render(buffer, Card);

        Assert.All(buffer.Skip(Card * 2), sample => Assert.Equal(0.75f, sample));
    }

    /// <summary>
    /// A plugin handing back nonsense. What matters is that the mixer does not throw over it and
    /// that the next block, with the plugin gone, is clean again: a bad number must not be left
    /// somewhere that outlives the thing that made it.
    /// </summary>
    /// <summary>
    /// Nonsense never reaches the card, not even while it is being made.
    /// </summary>
    /// <remarks>
    /// The test below says a poisoned block does not outlive the plugin that poisoned it, which
    /// is a different and weaker promise: it renders with the poison, takes it away, and looks
    /// at the block after. **This looks at the block during**, which is the one that reaches
    /// somebody's speakers.
    ///
    /// It went out unchanged before. The master's curve bends infinity and 1e30 down to one on
    /// its own, since both compare and both saturate, but a NaN fails the comparison against the
    /// knee, because every comparison with a NaN is false, and then Tanh hands one back. A
    /// buffer of NaN is undefined at the converters and commonly arrives as full scale noise,
    /// which is how tweeters and ears are damaged by software: the card itself is never at risk
    /// from what it is asked to play.
    /// </remarks>
    [Fact]
    public void Nonsense_never_reaches_the_card()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetInstrument(0, new Poison());

        var during = Block(mixer, Card);

        Assert.All(during, sample => Assert.True(float.IsFinite(sample), "a sample of " + sample + " left the mixer"));
    }

    /// <summary>Every shape of nonsense is bent or silenced, none of it passed on.</summary>
    /// <remarks>
    /// Silence for anything that is not a real number, NaN and both infinities alike, since none
    /// of them is a loud sample: they are the absence of one. Everything that is a number is
    /// bent to within full scale however large it is, so a sample of 1e30 is music that was too
    /// loud and comes back as one rather than as nothing.
    /// </remarks>
    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(1e30f, 1f)]
    [InlineData(-1e30f, -1f)]
    [InlineData(0.5f, 0.5f)]
    public void The_last_stage_lets_nothing_dangerous_out(float given, float expected)
    {
        Assert.Equal(expected, TrackMixer.SoftClip(given), 3);
    }

    /// <summary>And a poisoned block does not outlive the plugin that poisoned it.</summary>
    [Fact]
    public void Nonsense_from_a_plugin_does_not_outlive_it()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetInstrument(0, new Poison());

        Block(mixer, Card);

        mixer.SetInstrument(0, null);
        mixer.NoteOn(1, 0, Loud(), new Note(60), 1f, 0f);

        var after = Block(mixer, Card);

        Assert.All(after, sample => Assert.True(float.IsFinite(sample)));
    }

    // ---- Being asked for more than it has ---------------------------------------------

    /// <summary>
    /// More notes than the mix can hold. The oldest is taken rather than the list growing, and
    /// the block that comes out is still a block.
    /// </summary>
    [Fact]
    public void More_voices_than_it_holds_steals_rather_than_grows()
    {
        var mixer = new TrackMixer(Rate);

        for (int note = 0; note < TrackMixer.MaxVoices * 3; note++)
            mixer.NoteOn(note % Tracks, note % Song.MaxNoteColumns, Loud(), new Note(36 + note % 40), 1f, 0f);

        var buffer = Block(mixer, Ahead);

        Assert.All(buffer, sample => Assert.True(float.IsFinite(sample)));
        Assert.Contains(buffer, sample => sample != 0f);
    }

    /// <summary>
    /// A track number off either end of the mix is refused rather than indexing something. Every
    /// buffer, level and ducker here is indexed by that number, so this is the shape of fault
    /// that reads as the mixer being haunted.
    /// </summary>
    [Fact]
    public void A_track_number_off_the_end_is_refused()
    {
        var mixer = Sounding();

        foreach (int track in new[] { -1, -100, Tracks, Tracks + 5, int.MaxValue, int.MinValue })
        {
            mixer.NoteOn(track, 0, Loud(), new Note(60), 1f, 0f);
            mixer.NoteOff(track);
            mixer.SetLevels(track, 0, 1f, 0f);
            mixer.SetInsert(track, new Quiet());
            mixer.SetInstrument(track, new Silent());
            mixer.SetDucking(track, 0.5, 0, 50);
            mixer.MoveTrack(track, 0);
            mixer.MoveTrack(0, track);

            Assert.Equal(0f, mixer.GetTrackLevel(track));
        }

        var buffer = Block(mixer, Card);

        Assert.Contains(buffer, sample => sample != 0f);
    }

    /// <summary>
    /// A note column off the end of what a track can have, for the same reason: the plugin note
    /// record is one per track per column and is indexed by both.
    /// </summary>
    [Fact]
    public void A_note_column_off_the_end_is_refused()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetInstrument(0, new Silent());

        foreach (int column in new[] { -1, Song.MaxNoteColumns, Song.MaxNoteColumns + 9, int.MaxValue })
        {
            mixer.NoteOn(0, column, Loud(), new Note(60), 1f, 0f);
            mixer.PluginNoteOn(0, column, new Note(60), 1f, 0f, VoiceEnding.Cut);
            mixer.PluginNoteOff(0, column);
            mixer.NoteOff(0, column);
            mixer.SetLevels(0, column, 1f, 0f);
        }

        var buffer = Block(mixer, Card);

        Assert.All(buffer, sample => Assert.True(float.IsFinite(sample)));
    }

    // ---- Fakes ------------------------------------------------------------------------

    /// <summary>
    /// The parts of a plugin that have nothing to do with audio, answered once so the fakes
    /// below are only the one thing each is about.
    /// </summary>
    /// <remarks>
    /// A plugin is a parameter set and a thing to be disposed as well as a thing that renders,
    /// and none of that is what is being asked about here.
    /// </remarks>
    private abstract class Plugin : IPluginInstrument
    {
        /// <inheritdoc/>
        public PluginInfo Info { get; } = new("fake", "Fake", "", "", "");

        /// <inheritdoc/>
        public virtual void NoteOn(int semitone, float velocity) { }

        /// <inheritdoc/>
        public virtual void NoteOff(int semitone) { }

        /// <inheritdoc/>
        public virtual void AllNotesOff() { }

        /// <inheritdoc/>
        public abstract void Render(float[] buffer, int frames);

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
        public void Dispose() { }
    }

    /// <summary>A plugin that throws whenever it is asked for audio.</summary>
    private sealed class Angry : Plugin
    {
        /// <inheritdoc/>
        public override void Render(float[] buffer, int frames) => throw new InvalidOperationException("no");
    }

    /// <summary>A plugin that makes no sound and no trouble.</summary>
    private sealed class Silent : Plugin
    {
        /// <inheritdoc/>
        public override void Render(float[] buffer, int frames) { }
    }

    /// <summary>A plugin that fills everything it can see rather than the block it was given.</summary>
    private sealed class Greedy : Plugin
    {
        /// <inheritdoc/>
        public override void Render(float[] buffer, int frames) => Array.Fill(buffer, 0.5f);
    }

    /// <summary>A plugin that hands back numbers that are not numbers.</summary>
    private sealed class Poison : Plugin
    {
        /// <inheritdoc/>
        public override void Render(float[] buffer, int frames)
        {
            for (int i = 0; i < Math.Min(buffer.Length, frames * 2); i++)
                buffer[i] = i % 3 == 0 ? float.NaN : i % 3 == 1 ? float.PositiveInfinity : 1e30f;
        }
    }

    /// <summary>A plugin that stays inside a block until it is let go of.</summary>
    private sealed class SlowInstrument(ManualResetEventSlim inside, ManualResetEventSlim carryOn) : Plugin
    {
        /// <inheritdoc/>
        public override void Render(float[] buffer, int frames)
        {
            inside.Set();
            carryOn.Wait(5000);
        }
    }

    /// <summary>An insert that throws whenever it is handed a block.</summary>
    private sealed class Furious : IAudioInsert
    {
        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) => throw new InvalidOperationException("no");
    }

    /// <summary>An insert that does nothing at all, for churning the chain about.</summary>
    private sealed class Quiet : IAudioInsert
    {
        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) { }
    }

    /// <summary>
    /// A block of a different length does not rebuild the buffers.
    /// </summary>
    /// <remarks>
    /// The sound card asks for what it asks for, and a real session's log reads "the tracker stream
    /// is asking for between 8 and 529 frames at a time": with the buffers sized to the last block
    /// rather than the largest, that reallocated the loose bus and one bus per sounding track on
    /// very nearly every callback, which is a collection running inside the mix and a stutter you
    /// can hear on a song with three notes in it.
    ///
    /// Counted rather than reasoned about: the thread's own allocation total over a run of blocks
    /// whose lengths keep changing. A warm-up pass first, since the first block at any size builds
    /// what it needs and is allowed to. Four voices, because a mixer with nothing sounding rests
    /// before it reaches the buffers and would have passed this while the fault was still there.
    ///
    /// Checked by putting the fault back: twelve blocks allocate **0** bytes with the buffers
    /// grown, and **300,544** with them sized to the last block.
    /// </remarks>
    [Fact]
    public void A_different_block_size_does_not_rebuild_the_buffers()
    {
        var mixer = new TrackMixer(Rate);
        var buffer = new float[1024 * 2];

        int[] sizes = { 1024, 8, 64, 449, 529, 256 };

        for (int track = 0; track < 4; track++)
            mixer.NoteOn(track, 0, Loud(), new Note(48 + track), 1f, 0f);

        foreach (var frames in sizes) mixer.Render(buffer, frames);

        long before = GC.GetAllocatedBytesForCurrentThread();

        foreach (var frames in sizes) mixer.Render(buffer, frames);
        foreach (var frames in sizes) mixer.Render(buffer, frames);

        long taken = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(taken < 8192, "twelve blocks of shifting size allocated " + taken + " bytes");
    }
}
