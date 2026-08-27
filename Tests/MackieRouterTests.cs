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
/// </remarks>
public class MackieRouterTests
{
    private const string Desk = "Minilab3 MCU/HUI";

    private static (MidiMackieRouter Router, JingleBox2.Tests.Desk Mixer) Wired(int tracks = 16)
    {
        var mixer = new JingleBox2.Tests.Desk(tracks);

        return (new MidiMackieRouter(mixer, () => tracks), mixer);
    }

    private static MidiMessage Fader(int strip, int position) => new()
    {
        Device = Desk, Type = MidiMessageType.PitchBend,
        Channel = strip + 1, Value = 0, Data = position, IsOn = false
    };

    private static MidiMessage Pot(int strip, int value) => new()
    {
        Device = Desk, Type = MidiMessageType.ControlChange,
        Channel = 1, Value = 0x10 + strip, Data = value, IsOn = value > 0
    };

    private static MidiMessage Press(int note, bool down = true) => new()
    {
        Device = Desk, Type = MidiMessageType.Note,
        Channel = 1, Value = note, Data = down ? 127 : 0, IsOn = down
    };

    [Fact]
    public void A_fader_is_a_tracks_level_and_lands_on_it()
    {
        var (router, mixer) = Wired();

        // Fourteen bits, which is the whole reason a fader is sent as pitch bend: a hundred
        // millimetres of travel wants more than a hundred and twenty eight positions.
        router.Handle(Fader(2, 16383));

        Assert.Equal(1.0, mixer.At(2).Value, 4);

        router.Handle(Fader(2, 0));

        Assert.Equal(0.0, mixer.At(2).Value, 4);
    }

    [Fact]
    public void And_it_lands_rather_than_picking_up()
    {
        var (router, mixer) = Wired();

        // Every other position-reporting control here is picked up. This one is not, and that
        // is not an inconsistency: the fader is motorised and has already been driven to where
        // the parameter is by MackieSurface, so picking up would mean hunting for a value it is
        // sitting on.
        router.Handle(Fader(0, 4096));

        Assert.Equal(0.25, mixer.At(0).Value, 3);
        Assert.Equal(1, mixer.At(0).Writes);
    }

    [Fact]
    public void Eight_faders_reach_eight_different_tracks()
    {
        var (router, mixer) = Wired();

        for (int strip = 0; strip < 8; strip++) router.Handle(Fader(strip, 16383));

        for (int strip = 0; strip < 8; strip++) Assert.Equal(1.0, mixer.At(strip).Value, 4);
    }

    [Fact]
    public void The_master_fader_moves_nothing_because_there_is_no_master()
    {
        var (router, mixer) = Wired();

        // Channel nine on every surface that speaks this. Named in the log rather than ignored,
        // so a fader that does nothing says why.
        router.Handle(Fader(8, 16383));

        Assert.Empty(mixer.Asked);
    }

    [Fact]
    public void A_knob_says_how_far_it_moved_and_never_where_it_is()
    {
        var (router, mixer) = Wired();

        // Bit six is the direction and the six below it are how far, counted since the last
        // message. A full sixty three ticks is the whole range.
        router.Handle(Pot(0, 63));

        Assert.Equal(1.0, mixer.At(0, MixControl.Pan).Value, 3);

        router.Handle(Pot(0, 0x40 | 63));

        Assert.Equal(-1.0, mixer.At(0, MixControl.Pan).Value, 3);
    }

    [Fact]
    public void A_knob_sending_nothing_means_one_tick()
    {
        var (router, mixer) = Wired();

        // Some surfaces send nought when they mean one. Read literally the knob would be dead.
        router.Handle(Pot(1, 0));

        Assert.True(mixer.At(1, MixControl.Pan).Value > 0);
    }

    [Theory]
    [InlineData(0x08, MixControl.Solo)]
    [InlineData(0x10, MixControl.Mute)]
    public void A_strip_button_turns_its_switch_over(int from, MixControl what)
    {
        var (router, mixer) = Wired();

        router.Handle(Press(from + 3));

        Assert.Equal(1.0, mixer.At(3, what).Value, 3);

        // A press says it was pressed and nothing else. There is no on and off in one.
        router.Handle(Press(from + 3));

        Assert.Equal(0.0, mixer.At(3, what).Value, 3);
    }

    [Fact]
    public void The_release_does_nothing()
    {
        var (router, mixer) = Wired();

        router.Handle(Press(0x10, down: false));

        Assert.Empty(mixer.Asked);
    }

    [Fact]
    public void Banking_moves_the_eight_strips_along_the_tracks()
    {
        var (router, mixer) = Wired(tracks: 16);

        Assert.Equal(0, router.Bank);

        router.Handle(Press(0x2F));

        Assert.Equal(8, router.Bank);

        // So the leftmost fader is now track nine.
        router.Handle(Fader(0, 16383));

        Assert.Equal(1.0, mixer.At(8).Value, 4);

        router.Handle(Press(0x2E));

        Assert.Equal(0, router.Bank);
    }

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

    [Fact]
    public void Banking_stops_at_both_ends_rather_than_wrapping()
    {
        // A desk you can get lost on is worse than one that will not move, and the number of
        // tracks is not printed anywhere on the hardware to count against.
        var (router, _) = Wired(tracks: 12);

        for (int at = 0; at < 5; at++) router.Handle(Press(0x2F));

        Assert.Equal(4, router.Bank);

        for (int at = 0; at < 5; at++) router.Handle(Press(0x2E));

        Assert.Equal(0, router.Bank);
    }

    [Fact]
    public void A_desk_with_more_strips_than_there_are_tracks_does_not_bank_at_all()
    {
        var (router, _) = Wired(tracks: 4);

        router.Handle(Press(0x2F));

        Assert.Equal(0, router.Bank);
    }

    [Theory]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    [InlineData(0x5D)]
    [InlineData(0x5E)]
    [InlineData(0x5F)]
    public void The_transport_is_not_read_here(int note)
    {
        // It arrives on this same port and the transport router already answers it. Answering
        // twice would stop what the press had just started.
        var (router, mixer) = Wired();

        router.Handle(Press(note));

        Assert.Empty(mixer.Asked);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x18)]
    [InlineData(0x20)]
    [InlineData(0x68)]
    public void The_rows_with_nothing_to_do_move_nothing(int from)
    {
        // Record arm, select, a knob pressed, and a hand landing on a fader. Named in the log
        // so a button that does nothing says so, and doing nothing.
        var (router, mixer) = Wired();

        router.Handle(Press(from + 2));

        Assert.Empty(mixer.Asked);
    }

    [Fact]
    public void A_fader_past_the_end_of_the_song_reaches_nothing()
    {
        var (router, mixer) = Wired(tracks: 4);

        router.Handle(Fader(6, 16383));

        // It asked, and was answered with nothing, which is what a strip pointed past the last
        // track should be.
        Assert.Single(mixer.Asked);
        Assert.Equal(6, mixer.Asked[0].Track);
    }

    [Fact]
    public void Nothing_at_all_is_read_as_nothing() =>
        new MidiMackieRouter(new JingleBox2.Tests.Desk(), () => 8).Handle(null);
}
