using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Config;
using JingleBox2.UI;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The mixer desk: the four strips that belong to this machine rather than to a song.
/// </summary>
/// <remarks>
/// **They were written down nowhere at all.** A track's strip is the music and travels in the
/// song; the recording input, a take being auditioned, the pads together and what leaves the
/// machine are about the room this is being run in, and they lived as fields on the engine's
/// busses and in no file. Set the master fader because the speakers in this room are loud, and it
/// was back at unity the next morning with nothing saying why.
///
/// The other half is that they are not two copies. The value is the desk's and the bus is told,
/// so there is nothing to keep in step.
/// </remarks>
public sealed class MixerDeskTests
{
    /// <summary>A bus that remembers what it was set to and makes no sound.</summary>
    private sealed class Quiet : IOutputBus
    {
        /// <inheritdoc/>
        public float Level { get; set; } = 1f;

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public bool IsOpen => true;

        /// <inheritdoc/>
        public int Handle => 0;

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public int Sources => 0;

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0f, 0f);

        /// <inheritdoc/>
        public bool Add(int channel) => true;

        /// <inheritdoc/>
        public void Remove(int channel) { }

        /// <inheritdoc/>
        public bool Holds(int channel) => false;

        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> channels) { }

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool decoding) => true;

        /// <inheritdoc/>
        public void Close() { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>The desk over four quiet busses and a settings block of its own.</summary>
    private sealed class Bench
    {
        /// <summary>What the settings file would hold.</summary>
        public AppConfig Settings { get; } = new();

        /// <summary>How many times the settings were said to have moved.</summary>
        public int Said { get; private set; }

        /// <summary>The four busses, in the order the desk draws them.</summary>
        public Quiet Monitor { get; } = new();

        /// <inheritdoc cref="Monitor"/>
        public Quiet Takes { get; } = new();

        /// <inheritdoc cref="Monitor"/>
        public Quiet Pads { get; } = new();

        /// <inheritdoc cref="Monitor"/>
        public Quiet Output { get; } = new();

        /// <summary>What the recording input's own fader is, which is not a bus's level.</summary>
        public double InputGain { get; private set; }

        /// <summary>The desk itself.</summary>
        public MixerDesk Desk { get; }

        /// <summary>Builds it.</summary>
        public Bench()
        {
            var block = new Counting(Settings, () => Said++);

            Desk = new MixerDesk(
                block, Monitor, Takes, Pads, Output, new GainScale(),
                () => InputGain,
                value => InputGain = value);
        }

        /// <summary>A settings block that counts how often it was told something moved.</summary>
        private sealed class Counting : JingleBox2.Config.Interfaces.ISettingsBlock
        {
            /// <summary>What to do when it moves.</summary>
            private readonly Action _moved;

            /// <summary>Over a document and a counter.</summary>
            /// <param name="config">The document.</param>
            /// <param name="moved">What to do when it moves.</param>
            public Counting(AppConfig config, Action moved)
            {
                Config = config;
                _moved = moved;
            }

            /// <inheritdoc/>
            public AppConfig Config { get; }

            /// <inheritdoc/>
            public string Name => "Settings";

            /// <inheritdoc/>
            public bool Kept => true;

            /// <inheritdoc/>
            public event Action? Changed;

            /// <inheritdoc/>
            public void Moved()
            {
                _moved();

                Changed?.Invoke();
            }
        }
    }

    /// <summary>A level set on the desk is in the settings and on the bus.</summary>
    [Fact]
    public void A_level_is_written_down_and_told_to_the_bus()
    {
        var bench = new Bench();

        bench.Desk.Master.Level = -6;

        Assert.Equal(-6, bench.Settings.MixerDesk.Master.Level);
        Assert.Equal(-6, bench.Desk.Master.Level);
        Assert.True(bench.Output.Level < 0.6f);
        Assert.True(bench.Said > 0);
    }

    /// <summary>So is a pan and a mute.</summary>
    [Fact]
    public void A_pan_and_a_mute_are_written_down_and_told()
    {
        var bench = new Bench();

        bench.Desk.Pads.Pan = -0.5;
        bench.Desk.Pads.Mute = true;

        Assert.Equal(-0.5, bench.Settings.MixerDesk.Pads.Pan);
        Assert.Equal(-0.5, bench.Pads.Pan);
        Assert.True(bench.Settings.MixerDesk.Pads.Mute);
        Assert.True(bench.Pads.Mute);
    }

    /// <summary>
    /// **A solo is written down and told to nobody**, since it means only this and that is worked
    /// out over the whole row.
    /// </summary>
    [Fact]
    public void A_solo_is_written_down_and_left_to_the_row()
    {
        var bench = new Bench();

        bench.Desk.Play.Solo = true;

        Assert.True(bench.Settings.MixerDesk.Play.Solo);
        Assert.False(bench.Takes.Mute);
    }

    /// <summary>
    /// **Everything the settings hold goes back onto the busses**, which is what a start is.
    /// </summary>
    [Fact]
    public void A_desk_that_was_left_somewhere_comes_back_there()
    {
        var bench = new Bench();

        bench.Settings.MixerDesk.Master.Level = -12;
        bench.Settings.MixerDesk.Pads.Pan = 0.25;
        bench.Settings.MixerDesk.Play.Mute = true;

        bench.Desk.Restore();

        Assert.True(bench.Output.Level < 0.3f);
        Assert.Equal(0.25, bench.Pads.Pan);
        Assert.True(bench.Takes.Mute);
    }

    /// <summary>
    /// The recording input's fader is not a bus's level, and is left to whoever owns it.
    /// </summary>
    /// <remarks>
    /// What it moves is the gain on what is coming in, before anything is written, so it decides
    /// what a take holds rather than what the desk sends out. That is why the strip says Gain and
    /// why the number lives where every settings file already written has it.
    /// </remarks>
    [Fact]
    public void The_inputs_fader_is_its_gain_and_not_a_bus()
    {
        var bench = new Bench();

        bench.Desk.In.Level = 6.5;

        Assert.Equal(6.5, bench.InputGain);
        Assert.Equal(6.5, bench.Desk.In.Level);
        Assert.Equal(0, bench.Settings.MixerDesk.In.Level);
        Assert.Equal(1f, bench.Monitor.Level);
    }

    /// <summary>But the rest of that strip is the desk's like any other.</summary>
    [Fact]
    public void The_rest_of_the_inputs_strip_is_the_desks()
    {
        var bench = new Bench();

        bench.Desk.In.Pan = 0.75;

        Assert.Equal(0.75, bench.Settings.MixerDesk.In.Pan);
        Assert.Equal(0.75, bench.Monitor.Pan);
    }

    /// <summary>Setting something to what it already is says nothing and moves nothing.</summary>
    /// <remarks>
    /// A fader hands its value back whenever it is rebuilt, which is every time a page is drawn
    /// again, and a page switch is not somebody moving the desk.
    /// </remarks>
    [Fact]
    public void Setting_it_to_what_it_already_is_says_nothing()
    {
        var bench = new Bench();

        bench.Desk.Master.Level = -6;

        int said = bench.Said;

        bench.Desk.Master.Level = -6;
        bench.Desk.Master.Pan = 0;
        bench.Desk.Master.Mute = false;

        Assert.Equal(said, bench.Said);
    }

    /// <summary>And every strip is on the list, for whatever walks them.</summary>
    [Fact]
    public void Every_strip_is_on_the_list()
    {
        var bench = new Bench();

        Assert.Equal(4, bench.Desk.Strips.Count);
        Assert.Contains(bench.Desk.In, bench.Desk.Strips);
        Assert.Contains(bench.Desk.Master, bench.Desk.Strips);
    }
}
