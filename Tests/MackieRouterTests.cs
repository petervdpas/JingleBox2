using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A control surface that needs no file and no learning, because the protocol says what every
/// control on it is.
/// </summary>
/// <remarks>
/// Which is why none of the sensing the rest of this application does appears here. The numbers
/// are Mackie Control's, read off Ardour's implementation of the same protocol under the same
/// licence.
///
/// The order the tests come in is the order a hand meets the desk: the faders first, then the
/// knobs, then the strip buttons, then banking, and last the rows this mixer deliberately has
/// nothing to do with.
/// </remarks>
public class MackieRouterTests
{
    /// <summary>The port a MiniLab 3 speaks Mackie Control on, and where these arrive.</summary>
    private const string Desk = "Minilab3 MCU/HUI";

    /// <summary>A router over a mixer that writes down everything it was asked for.</summary>
    /// <remarks>
    /// The track count is handed over as well as built into the mixer, because the router asks
    /// how many there are to work out how far the eight strips may bank.
    /// </remarks>
    private static (MidiMackieRouter Router, JingleBox2.Tests.Desk Mixer) Wired(int tracks = 16)
    {
        var mixer = new JingleBox2.Tests.Desk(tracks);

        return (new MidiMackieRouter(mixer, () => tracks), mixer);
    }

    /// <summary>One strip's fader: pitch bend on the strip's own channel, fourteen bits.</summary>
    private static MidiMessage Fader(int strip, int position) => new()
    {
        Device = Desk, Type = MidiMessageType.PitchBend,
        Channel = strip + 1, Value = 0, Data = position, IsOn = false
    };

    /// <summary>
    /// One strip's knob: relative controllers 0x10 to 0x17 on channel 1, the direction in bit 6
    /// and the ticks in the six below it.
    /// </summary>
    private static MidiMessage Pot(int strip, int value) => new()
    {
        Device = Desk, Type = MidiMessageType.ControlChange,
        Channel = 1, Value = 0x10 + strip, Data = value, IsOn = value > 0
    };

    /// <summary>A button, which is a note at 127 pressed and at 0 let go of.</summary>
    private static MidiMessage Press(int note, bool down = true) => new()
    {
        Device = Desk, Type = MidiMessageType.Note,
        Channel = 1, Value = note, Data = down ? 127 : 0, IsOn = down
    };

    /// <summary>A fader drives its own track's level, from silence to the top of the throw.</summary>
    /// <remarks>
    /// Fourteen bits, which is the whole reason a fader is sent as pitch bend: a hundred
    /// millimetres of travel wants more than a hundred and twenty eight positions.
    /// </remarks>
    [Fact]
    public void A_fader_is_a_tracks_level_and_lands_on_it()
    {
        var (router, mixer) = Wired();

        router.Handle(Fader(2, 16383));

        Assert.Equal(1.0, mixer.At(2).Value, 4);

        router.Handle(Fader(2, 0));

        Assert.Equal(0.0, mixer.At(2).Value, 4);
    }

    /// <summary>The first position that arrives is taken, and one write is all it takes.</summary>
    /// <remarks>
    /// Every other position-reporting control here is picked up. This one is not, and that
    /// is not an inconsistency: the fader is motorised and has already been driven to where
    /// the parameter is by MackieSurface, so picking up would mean hunting for a value it is
    /// sitting on.
    /// </remarks>
    [Fact]
    public void And_it_lands_rather_than_picking_up()
    {
        var (router, mixer) = Wired();

        router.Handle(Fader(0, 4096));

        Assert.Equal(0.25, mixer.At(0).Value, 3);
        Assert.Equal(1, mixer.At(0).Writes);
    }

    /// <summary>The eight strips are eight tracks and not eight ways to the same one.</summary>
    [Fact]
    public void Eight_faders_reach_eight_different_tracks()
    {
        var (router, mixer) = Wired();

        for (int strip = 0; strip < 8; strip++) router.Handle(Fader(strip, 16383));

        for (int strip = 0; strip < 8; strip++) Assert.Equal(1.0, mixer.At(strip).Value, 4);
    }

    /// <summary>The ninth fader, which every desk of this kind has, reaches nothing here.</summary>
    /// <remarks>
    /// Channel nine on every surface that speaks this. Named in the log rather than ignored,
    /// so a fader that does nothing says why.
    /// </remarks>
    [Fact]
    public void The_master_fader_moves_nothing_because_there_is_no_master()
    {
        var (router, mixer) = Wired();

        router.Handle(Fader(8, 16383));

        Assert.Empty(mixer.Asked);
    }

    /// <summary>A knob is read as movement since the last message, in either direction.</summary>
    /// <remarks>
    /// Bit six is the direction and the six below it are how far, counted since the last
    /// message. A full sixty three ticks is the whole range.
    /// </remarks>
    [Fact]
    public void A_knob_says_how_far_it_moved_and_never_where_it_is()
    {
        var (router, mixer) = Wired();

        router.Handle(Pot(0, 63));

        Assert.Equal(1.0, mixer.At(0, MixControl.Pan).Value, 3);

        router.Handle(Pot(0, 0x40 | 63));

        Assert.Equal(-1.0, mixer.At(0, MixControl.Pan).Value, 3);
    }

    /// <summary>A knob reporting no ticks at all has still moved by one.</summary>
    /// <remarks>
    /// Some surfaces send nought when they mean one. Read literally the knob would be dead.
    /// </remarks>
    [Fact]
    public void A_knob_sending_nothing_means_one_tick()
    {
        var (router, mixer) = Wired();

        router.Handle(Pot(1, 0));

        Assert.True(mixer.At(1, MixControl.Pan).Value > 0);
    }

    /// <summary>Solo is note 0x08 plus the strip and mute is 0x10 plus the strip.</summary>
    /// <remarks>
    /// A press says it was pressed and nothing else. There is no on and off in one, so the
    /// switch is turned over by whichever press arrives.
    /// </remarks>
    [Theory]
    [InlineData(0x08, MixControl.Solo)]
    [InlineData(0x10, MixControl.Mute)]
    public void A_strip_button_turns_its_switch_over(int from, MixControl what)
    {
        var (router, mixer) = Wired();

        router.Handle(Press(from + 3));

        Assert.Equal(1.0, mixer.At(3, what).Value, 3);

        router.Handle(Press(from + 3));

        Assert.Equal(0.0, mixer.At(3, what).Value, 3);
    }

    /// <summary>The note off half of a press is ignored, or every press would count twice.</summary>
    [Fact]
    public void The_release_does_nothing()
    {
        var (router, mixer) = Wired();

        router.Handle(Press(0x10, down: false));

        Assert.Empty(mixer.Asked);
    }

    /// <summary>0x2F and 0x2E move the window of eight strips a bank at a time.</summary>
    /// <remarks>
    /// After a bank right the leftmost fader is track nine, which is the whole point of the
    /// window: eight strips over a song with more tracks than that.
    /// </remarks>
    [Fact]
    public void Banking_moves_the_eight_strips_along_the_tracks()
    {
        var (router, mixer) = Wired(tracks: 16);

        Assert.Equal(0, router.Bank);

        router.Handle(Press(0x2F));

        Assert.Equal(8, router.Bank);

        router.Handle(Fader(0, 16383));

        Assert.Equal(1.0, mixer.At(8).Value, 4);

        router.Handle(Press(0x2E));

        Assert.Equal(0, router.Bank);
    }

    /// <summary>0x30 and 0x31 move the window by one track rather than by eight.</summary>
    [Fact]
    public void One_channel_at_a_time_as_well()
    {
        var (router, _) = Wired();

        router.Handle(Press(0x31));
        router.Handle(Press(0x31));

        Assert.Equal(2, router.Bank);

        router.Handle(Press(0x30));

        Assert.Equal(1, router.Bank);
    }

    /// <summary>The window stops at the first track and at the last.</summary>
    /// <remarks>
    /// A desk you can get lost on is worse than one that will not move, and the number of
    /// tracks is not printed anywhere on the hardware to count against.
    /// </remarks>
    [Fact]
    public void Banking_stops_at_both_ends_rather_than_wrapping()
    {
        var (router, _) = Wired(tracks: 12);

        for (int at = 0; at < 5; at++) router.Handle(Press(0x2F));

        Assert.Equal(4, router.Bank);

        for (int at = 0; at < 5; at++) router.Handle(Press(0x2E));

        Assert.Equal(0, router.Bank);
    }

    /// <summary>A song shorter than the desk is wide has nowhere to bank to.</summary>
    [Fact]
    public void A_desk_with_more_strips_than_there_are_tracks_does_not_bank_at_all()
    {
        var (router, _) = Wired(tracks: 4);

        router.Handle(Press(0x2F));

        Assert.Equal(0, router.Bank);
    }

    /// <summary>The five transport notes are refused here by name.</summary>
    /// <remarks>
    /// They arrive on this same port and the transport router already answers them. Answering
    /// twice would stop what the press had just started.
    /// </remarks>
    [Theory]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    [InlineData(0x5D)]
    [InlineData(0x5E)]
    [InlineData(0x5F)]
    public void The_transport_is_not_read_here(int note)
    {
        var (router, mixer) = Wired();

        router.Handle(Press(note));

        Assert.Empty(mixer.Asked);
    }

    /// <summary>Four button rows this desk sends and this mixer has no answer for.</summary>
    /// <remarks>
    /// Record arm, select, a knob pressed, and a hand landing on a fader. Named in the log
    /// so a button that does nothing says so, and doing nothing.
    /// </remarks>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x18)]
    [InlineData(0x20)]
    [InlineData(0x68)]
    public void The_rows_with_nothing_to_do_move_nothing(int from)
    {
        var (router, mixer) = Wired();

        router.Handle(Press(from + 2));

        Assert.Empty(mixer.Asked);
    }

    /// <summary>A strip beyond the end of the song moves nothing and does not throw.</summary>
    /// <remarks>
    /// It asked, and was answered with nothing, which is what a strip pointed past the last
    /// track should be.
    /// </remarks>
    [Fact]
    public void A_fader_past_the_end_of_the_song_reaches_nothing()
    {
        var (router, mixer) = Wired(tracks: 4);

        router.Handle(Fader(6, 16383));

        Assert.Single(mixer.Asked);
        Assert.Equal(6, mixer.Asked[0].Track);
    }

    /// <summary>Nothing arriving is read as nothing rather than as a fault.</summary>
    [Fact]
    public void Nothing_at_all_is_read_as_nothing() =>
        new MidiMackieRouter(new JingleBox2.Tests.Desk(), () => 8).Handle(null);
}
