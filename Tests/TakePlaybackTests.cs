using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Waveform;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What auditioning a take does, with no sound card and no window under it.
/// </summary>
/// <remarks>
/// **The rule these are here for is that there is one path out.** A take goes on the take bus the
/// way a pad goes on the pad bus, and the player had a second way of playing for the case where
/// there was no bus: the channel played itself, which sends it to whatever output the calling
/// thread happens to hold rather than to the desk. That is a fork whose two halves answer
/// differently on two machines with nothing anywhere saying so, and from a chair the wrong half
/// is a take that runs its cursor and makes no sound.
///
/// So the half checked hardest here is the one that does nothing: a player with nowhere to put a
/// take opens no file at all.
/// </remarks>
public sealed class TakePlaybackTests
{
    /// <summary>What the source and the bus were asked to do, in the order they were asked.</summary>
    private sealed class Told
    {
        /// <summary>Every call, as one word and the channel it was about.</summary>
        public List<string> Did { get; } = new();
    }

    /// <summary>A recording that is however long it is told to be, and says what it was asked.</summary>
    private sealed class Fake : IRecordingSource
    {
        /// <summary>Where the calls are written down, shared with the bus.</summary>
        private readonly Told _told;

        /// <summary>How long every recording it opens turns out to be.</summary>
        private readonly double _seconds;

        /// <summary>The channel it hands out, which is nought where it opens nothing.</summary>
        private readonly int _channel;

        /// <summary>Takes what it should answer for a file it is given.</summary>
        /// <param name="told">Where the calls are written down.</param>
        /// <param name="seconds">How long a recording is.</param>
        /// <param name="channel">What <see cref="Open"/> answers.</param>
        public Fake(Told told, double seconds = 4, int channel = 7)
        {
            _told = told;
            _seconds = seconds;
            _channel = channel;
        }

        /// <summary>Every file it was asked to open, in order.</summary>
        public List<string> Opened { get; } = new();

        /// <summary>Every channel it was asked to let go of.</summary>
        public List<int> Closed { get; } = new();

        /// <summary>Where it was last sent, in seconds.</summary>
        public double Sought { get; private set; }

        /// <summary>Where it says it has got to.</summary>
        public double Position { get; set; }

        /// <summary>Whether it says it has run out.</summary>
        public bool Over { get; set; }

        /// <inheritdoc/>
        public int Open(string filePath, bool loops = false)
        {
            Opened.Add(filePath);

            return _channel;
        }

        /// <inheritdoc/>
        public void Close(int channel)
        {
            Closed.Add(channel);
            _told.Did.Add("close " + channel);
        }

        /// <inheritdoc/>
        public double Seconds(int channel) => _seconds;

        /// <inheritdoc/>
        public double At(int channel) => Position;

        /// <inheritdoc/>
        public void Seek(int channel, double seconds)
        {
            Sought = seconds;
            Position = seconds;
        }

        /// <inheritdoc/>
        public bool Ended(int channel) => Over;
    }

    /// <summary>A bus that is open or not, and takes a source or refuses it.</summary>
    private sealed class Bus : IOutputBus
    {
        /// <summary>Where the calls are written down, shared with the source.</summary>
        private readonly Told _told;

        /// <summary>Whether it says yes to a source.</summary>
        private readonly bool _takes;

        /// <summary>Takes whether it is open and whether it accepts anything.</summary>
        /// <param name="told">Where the calls are written down.</param>
        /// <param name="open">Whether it is open.</param>
        /// <param name="takes">Whether it takes a source it is handed.</param>
        public Bus(Told told, bool open = true, bool takes = true)
        {
            _told = told;
            IsOpen = open;
            _takes = takes;
        }

        /// <summary>What is on it.</summary>
        public HashSet<int> On { get; } = new();

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public bool IsOpen { get; }

        /// <inheritdoc/>
        public int Handle => IsOpen ? 99 : 0;

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public float Level { get; set; } = 1f;

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0, 0);

        /// <inheritdoc/>
        public int Sources => On.Count;

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => true;

        /// <inheritdoc/>
        public bool Add(int source)
        {
            if (!_takes) return false;

            On.Add(source);

            return true;
        }

        /// <inheritdoc/>
        public void Remove(int source)
        {
            On.Remove(source);
            _told.Did.Add("remove " + source);
        }

        /// <inheritdoc/>
        public bool Holds(int source) => On.Contains(source);

        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources)
        {
        }

        /// <inheritdoc/>
        public void Close()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>How long the file is said to be, which only has to be more than nought.</summary>
    private const long Frames = 44100;

    /// <summary>An ordinary audition: the file is opened once and the channel lands on the bus.</summary>
    [Fact]
    public void A_take_goes_on_the_bus_and_nowhere_else()
    {
        var told = new Told();
        var source = new Fake(told);
        var bus = new Bus(told);

        var player = new WaveformPlayer(source, bus);

        player.Play("take.wav", 0, 1, Frames);

        Assert.True(player.IsPlaying);
        Assert.Equal(new[] { "take.wav" }, source.Opened);
        Assert.True(bus.Holds(7));
    }

    /// <summary>
    /// A player that was built with nowhere to send a take plays nothing whatever it is asked.
    /// </summary>
    /// <remarks>
    /// The one that has to be asked for by name. There is no way to arrive here by leaving an
    /// argument out, which is what the constructor is for, so what is left to pin is that the
    /// deliberate case really is silent rather than merely unwired.
    /// </remarks>
    [Fact]
    public void A_player_with_nowhere_to_send_a_take_plays_nothing()
    {
        var player = WaveformPlayer.Silent();

        player.Play("take.wav", 0, 1, Frames);

        Assert.False(player.IsPlaying);
    }

    /// <summary>A bus that exists and is not open is nowhere to put it, and answers the same.</summary>
    [Fact]
    public void A_bus_that_is_not_open_is_the_same_answer()
    {
        var told = new Told();
        var source = new Fake(told);

        var player = new WaveformPlayer(source, new Bus(told, open: false));

        player.Play("take.wav", 0, 1, Frames);

        Assert.False(player.IsPlaying);
        Assert.Empty(source.Opened);
    }

    /// <summary>A refusal is not playing, and the channel is let go of rather than left behind.</summary>
    [Fact]
    public void A_bus_that_refuses_it_leaves_nothing_playing_and_nothing_open()
    {
        var told = new Told();
        var source = new Fake(told);

        var player = new WaveformPlayer(source, new Bus(told, takes: false));

        player.Play("take.wav", 0, 1, Frames);

        Assert.False(player.IsPlaying);
        Assert.Equal(new[] { 7 }, source.Closed);
    }

    /// <summary>A file that opens with nothing in it is closed again rather than sitting on the bus.</summary>
    [Fact]
    public void A_recording_with_no_length_in_it_is_let_go_of()
    {
        var told = new Told();
        var source = new Fake(told, seconds: 0);

        var player = new WaveformPlayer(source, new Bus(told));

        player.Play("take.wav", 0, 1, Frames);

        Assert.False(player.IsPlaying);
        Assert.Equal(new[] { 7 }, source.Closed);
    }

    /// <summary>A quarter of the way into eight seconds is two seconds, whatever a sample looks like.</summary>
    [Fact]
    public void The_region_is_read_in_seconds_off_the_recording()
    {
        var told = new Told();
        var source = new Fake(told, seconds: 8);

        var player = new WaveformPlayer(source, new Bus(told));

        player.Play("take.wav", 0.25, 0.5, Frames);

        Assert.Equal(2, source.Sought, 3);
    }

    /// <summary>Pulling the end back past what is being heard ends it and clears the bus.</summary>
    [Fact]
    public void Dragging_the_end_behind_the_position_stops_it()
    {
        var told = new Told();
        var source = new Fake(told, seconds: 8);
        var bus = new Bus(told);

        var player = new WaveformPlayer(source, bus);

        player.Play("take.wav", 0, 1, Frames);

        source.Position = 4;

        player.PlayUntil(0.25);

        Assert.False(player.IsPlaying);
        Assert.False(bus.Holds(7));
        Assert.Equal(new[] { 7 }, source.Closed);
    }

    /// <summary>Stopping leaves nothing on the bus and nothing open.</summary>
    [Fact]
    public void Stopping_takes_it_off_the_bus_and_lets_it_go()
    {
        var told = new Told();
        var source = new Fake(told);
        var bus = new Bus(told);

        var player = new WaveformPlayer(source, bus);

        player.Play("take.wav", 0, 1, Frames);
        player.Stop();

        Assert.False(player.IsPlaying);
        Assert.Empty(bus.On);
        Assert.Equal(new[] { 7 }, source.Closed);
    }

    /// <summary>
    /// It comes off the bus before it is freed, which is the one order that cannot be got wrong.
    /// </summary>
    /// <remarks>
    /// A source freed while the mixer still holds it leaves the add-on pointing at memory that has
    /// gone, on the thread that is mixing. Nothing about the other order looks wrong from a chair.
    /// </remarks>
    [Fact]
    public void It_comes_off_the_bus_before_it_is_freed()
    {
        var told = new Told();
        var source = new Fake(told);

        var player = new WaveformPlayer(source, new Bus(told));

        player.Play("take.wav", 0, 1, Frames);
        player.Stop();

        Assert.Equal(new[] { "remove 7", "close 7" }, told.Did);
    }

    /// <summary>A caller that could not read the file is believed rather than made to find out twice.</summary>
    [Fact]
    public void A_file_the_caller_could_not_read_is_not_opened_again()
    {
        var told = new Told();
        var source = new Fake(told);

        var player = new WaveformPlayer(source, new Bus(told));

        player.Play("take.wav", 0, 1, 0);

        Assert.False(player.IsPlaying);
        Assert.Empty(source.Opened);
    }
}
