using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What goes on the wire when this machine is the clock, and in what order.
/// </summary>
/// <remarks>
/// Every byte here is read by somebody else's hardware, so the order is as much of the contract
/// as the bytes are: a pointer after a continue tells a device to carry on from wherever it was
/// and then moves the mark, which is the wrong way round and puts the whole desk a chorus out.
///
/// No port and no device: the service is a bench that writes down what it was handed, which is
/// the whole reason the sending is a seam of its own.
/// </remarks>
public class MidiClockDeckTests
{
    /// <summary>A MIDI service that opens whatever it is asked for and keeps what it is sent.</summary>
    private sealed class Bench : IMidiService
    {
        /// <summary>Every message, in the order it was handed over, with the port it went to.</summary>
        public readonly List<(string Port, byte[] Bytes)> Sent = new();

        /// <summary>Which ports were opened ahead of anything being written to them.</summary>
        public readonly List<string> Opened = new();

        /// <summary>Names that will refuse to open, for the case where a device has gone.</summary>
        public readonly HashSet<string> Refuses = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every status byte sent, as one string, which is what most of these read.</summary>
        public string Statuses =>
            string.Join(" ", Sent.Select(one => one.Bytes[0].ToString("X2")));

        /// <inheritdoc/>
        public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();

        /// <inheritdoc/>
        public IReadOnlyList<string> GetOutputDevices() => new[] { "one", "two" };

        /// <inheritdoc/>
        public IReadOnlyList<string> OpenDevices => Array.Empty<string>();

        /// <inheritdoc/>
        public bool Open(string device) => false;

        /// <inheritdoc/>
        public bool OpenFor(string device)
        {
            if (Refuses.Contains(device)) return false;

            Opened.Add(device);

            return true;
        }

        /// <inheritdoc/>
        public bool Send(string device, byte[] bytes)
        {
            Sent.Add((device, bytes));

            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Accessors that do nothing rather than a field, since this bench only ever writes and
        /// an event nothing raises is a field nothing reads.
        /// </remarks>
        public event EventHandler<MidiMessage>? MessageReceived
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public void Close(string device)
        {
        }

        /// <inheritdoc/>
        public void CloseAll()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>A deck over a bench, driving those ports.</summary>
    private static (MidiClockDeck Deck, Bench Bench) Driving(params string[] ports)
    {
        var bench = new Bench();
        var deck = new MidiClockDeck(bench);

        deck.Drive(ports);

        return (deck, bench);
    }

    /// <summary>A fresh one drives nothing, so nothing anybody owns starts running.</summary>
    /// <remarks>
    /// The whole of why this is safe to ship. Clock arriving at a device nobody pointed it at is
    /// a device that runs when its owner did not ask.
    /// </remarks>
    [Fact]
    public void A_fresh_one_drives_nothing()
    {
        var bench = new Bench();
        var deck = new MidiClockDeck(bench);

        Assert.False(deck.IsDriving);

        deck.Play(0, 4);
        deck.Ticks(24);
        deck.Halt();

        Assert.Empty(bench.Sent);
    }

    /// <summary>Every port is opened before a byte is written to it.</summary>
    /// <remarks>
    /// The measured reason: opening an output costs 80 ms on a real port against 0.24 ms for a
    /// send, and a tick at 120 to the minute is 20.8 ms. An open on the first tick is four ticks
    /// missed on the thread that also triggers notes.
    /// </remarks>
    [Fact]
    public void Ports_are_opened_before_anything_is_sent()
    {
        var (_, bench) = Driving("one", "two");

        Assert.Equal(new[] { "one", "two" }, bench.Opened);
        Assert.Empty(bench.Sent);
    }

    /// <summary>A port that will not open is not driven, and the rest still are.</summary>
    [Fact]
    public void A_port_that_will_not_open_is_dropped()
    {
        var bench = new Bench();

        bench.Refuses.Add("gone");

        var deck = new MidiClockDeck(bench);

        deck.Drive(new[] { "one", "gone", "two" });
        deck.Ticks(1);

        Assert.True(deck.IsDriving);
        Assert.Equal(new[] { "one", "two" }, bench.Sent.Select(one => one.Port));
    }

    /// <summary>Told nothing, it stops driving, which is how the setting is turned off.</summary>
    [Fact]
    public void Told_nothing_it_drives_nothing()
    {
        var (deck, bench) = Driving("one");

        deck.Drive(null);

        Assert.False(deck.IsDriving);

        deck.Ticks(4);

        Assert.Empty(bench.Sent);

        deck.Drive(Array.Empty<string>());

        Assert.False(deck.IsDriving);
    }

    /// <summary>From the top it is a plain start, with no pointer.</summary>
    /// <remarks>
    /// A pointer of nought followed by a continue would say the same thing to a well behaved
    /// device and is two more messages for nothing; plain start is what the standard means here.
    /// </remarks>
    [Fact]
    public void From_the_top_it_is_a_start()
    {
        var (deck, bench) = Driving("one");

        deck.Play(0, 4);

        Assert.Equal("FA", bench.Statuses);
    }

    /// <summary>
    /// And from anywhere else it is a pointer and then a continue, in that order.
    /// </summary>
    /// <remarks>
    /// **The order is the assertion.** A continue means "from where I last told you", so the
    /// pointer has to have arrived first; sent the other way round the device carries on from
    /// wherever it was and only then learns where it should have been.
    /// </remarks>
    [Fact]
    public void From_further_down_it_is_a_pointer_and_a_continue()
    {
        var (deck, bench) = Driving("one");

        deck.Play(32, 4);

        Assert.Equal("F2 FB", bench.Statuses);
    }

    /// <summary>The pointer carries its position as two seven-bit halves, low one first.</summary>
    /// <remarks>
    /// Line 32 at four lines to the beat is 32 sixteenths, which fits in the low half; line 4000
    /// does not, and is what says the halves are the right way round. A pointer sent the other
    /// way about is a device starting in a plausible but wrong place, which is worse than one
    /// that plainly fails.
    /// </remarks>
    [Theory]
    [InlineData(32, 32, 0)]
    [InlineData(127, 127, 0)]
    [InlineData(128, 0, 1)]
    [InlineData(4000, 4000 & 0x7F, 4000 >> 7)]
    public void The_pointer_is_two_seven_bit_halves(int line, int low, int high)
    {
        var (deck, bench) = Driving("one");

        deck.Play(line, 4);

        var pointer = bench.Sent[0].Bytes;

        Assert.Equal(3, pointer.Length);
        Assert.Equal(0xF2, pointer[0]);
        Assert.Equal(low, pointer[1]);
        Assert.Equal(high, pointer[2]);
    }

    /// <summary>Ticks are ticks, one byte each, as many as were due.</summary>
    [Fact]
    public void Ticks_go_out_one_byte_each()
    {
        var (deck, bench) = Driving("one");

        deck.Ticks(6);

        Assert.Equal(6, bench.Sent.Count);
        Assert.All(bench.Sent, one => Assert.Equal(new byte[] { 0xF8 }, one.Bytes));
    }

    /// <summary>Nought or fewer sends nothing rather than throwing.</summary>
    [Fact]
    public void No_ticks_due_sends_nothing()
    {
        var (deck, bench) = Driving("one");

        deck.Ticks(0);
        deck.Ticks(-5);

        Assert.Empty(bench.Sent);
    }

    /// <summary>
    /// A caller that has lost an implausible stretch of time is caught up only so far.
    /// </summary>
    /// <remarks>
    /// Ticks are a count against a stopwatch, so a machine that was suspended, or stopped at a
    /// breakpoint, comes back and is told that tens of thousands are due. Writing them all would
    /// hold the clock thread for the length of it, which is the music stopping to explain that it
    /// is behind. Bounded at more than two beats, past which the other end has lost the plot
    /// whatever this does.
    /// </remarks>
    [Fact]
    public void An_implausible_catch_up_is_bounded()
    {
        var (deck, bench) = Driving("one");

        deck.Ticks(1000000);

        Assert.Equal(64, bench.Sent.Count);
    }

    /// <summary>Stopping says stop.</summary>
    [Fact]
    public void Halting_says_stop()
    {
        var (deck, bench) = Driving("one");

        deck.Halt();

        Assert.Equal("FC", bench.Statuses);
    }

    /// <summary>Every message goes to every port being driven.</summary>
    [Fact]
    public void Every_port_hears_everything()
    {
        var (deck, bench) = Driving("one", "two", "three");

        deck.Play(0, 4);
        deck.Ticks(2);
        deck.Halt();

        Assert.Equal(12, bench.Sent.Count);

        foreach (string port in new[] { "one", "two", "three" })
            Assert.Equal("FA F8 F8 FC",
                string.Join(" ", bench.Sent.Where(one => one.Port == port)
                    .Select(one => one.Bytes[0].ToString("X2"))));
    }

    /// <summary>A whole bar of a pass reads as one start and twenty four ticks a beat.</summary>
    /// <remarks>
    /// The shape a slave actually sees, end to end, rather than each message on its own.
    /// </remarks>
    [Fact]
    public void A_bar_reads_as_a_start_and_ninety_six_ticks()
    {
        var (deck, bench) = Driving("one");

        deck.Play(0, 4);

        for (int beat = 0; beat < 4; beat++) deck.Ticks(24);

        deck.Halt();

        Assert.Equal(98, bench.Sent.Count);
        Assert.Equal(0xFA, bench.Sent[0].Bytes[0]);
        Assert.Equal(96, bench.Sent.Count(one => one.Bytes[0] == 0xF8));
        Assert.Equal(0xFC, bench.Sent[^1].Bytes[0]);
    }

    /// <summary>
    /// The three pass-through members put the byte out and work nothing out again.
    /// </summary>
    /// <remarks>
    /// The half used when this machine is passing on a clock it is itself running on, where
    /// nothing may be re-derived. Read as bytes rather than as words, because what is being
    /// asserted is what leaves the port.
    /// </remarks>
    [Fact]
    public void Passing_on_puts_the_plain_bytes_out()
    {
        var bench = new Bench();
        var deck = new MidiClockDeck(bench);

        deck.Drive(new[] { "one" });

        deck.Begin();
        deck.Place(300);
        deck.Resume();

        Assert.Equal("FA F2 FB", bench.Statuses);

        var pointer = bench.Sent[1].Bytes;

        Assert.Equal(300, (pointer[1] & 0x7F) | ((pointer[2] & 0x7F) << 7));
    }

    /// <summary>A pointer past what the wire can hold is held at the top rather than wrapping.</summary>
    /// <remarks>
    /// Reached from the thread a port delivers on, so a throw would be that port's reader gone.
    /// Wrapping is the failure that matters: fourteen bits taken modulo would put a device at the
    /// top of a song at the moment the master said the end of one, which is a relocation landing
    /// somewhere nobody asked for rather than an error anybody sees.
    /// </remarks>
    [Fact]
    public void A_pointer_too_big_for_the_wire_is_held_at_the_top()
    {
        var bench = new Bench();
        var deck = new MidiClockDeck(bench);

        deck.Drive(new[] { "one" });

        deck.Place(40000);
        deck.Place(-5);

        var most = bench.Sent[0].Bytes;
        var least = bench.Sent[1].Bytes;

        Assert.Equal(16383, (most[1] & 0x7F) | ((most[2] & 0x7F) << 7));
        Assert.Equal(0, (least[1] & 0x7F) | ((least[2] & 0x7F) << 7));
    }

    /// <summary>Driving nothing, the pass-through half sends nothing either.</summary>
    [Fact]
    public void Passing_on_to_nothing_sends_nothing()
    {
        var bench = new Bench();
        var deck = new MidiClockDeck(bench);

        deck.Begin();
        deck.Resume();
        deck.Place(16);

        Assert.Empty(bench.Sent);
    }
}
