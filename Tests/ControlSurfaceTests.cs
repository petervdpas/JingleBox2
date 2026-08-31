using System.Collections.Generic;
using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which devices are read as a Mackie surface, and what a momentary button does to a switch.
/// </summary>
/// <remarks>
/// Both of these exist because one device broke on both at once, and the way it broke is worth
/// keeping in a test rather than in anybody's memory.
///
/// A nanoKONTROL2's transport buttons send plain controllers, so the only way to reach the
/// transport with one is to tick it for Transport in SETTINGS. That switch is what turns the
/// Mackie reading on, and Mackie's eight V-pots are continuous controllers 16 to 23, which is
/// exactly what a nanoKONTROL2's eight knobs send. So every knob was read twice, once as the
/// position it is and once as a count of notches it is not, and a knob turned slowly threw a pan
/// to either end. From a hand on the desk that reads as a knob that is far too sensitive, which
/// is precisely how it was reported.
///
/// And its thirty five buttons are all Momentary, which is in the device's scene and in no
/// mapping list anywhere. Followed as a value, a mute button muted a track while a thumb was on
/// it and unmuted it on the way up.
/// </remarks>
public class ControlSurfaceTests
{
    /// <summary>What is known about the controllers plugged in.</summary>
    private readonly IControllerProfiles _profiles = new ControllerProfiles();

    /// <summary>A nanoKONTROL2, whose file describes it fully and names no protocol.</summary>
    private const string Korg = "nanoKONTROL2 _ CTRL";

    /// <summary>A MiniLab 3's main port, where its knobs are.</summary>
    private const string LabMain = "Minilab3 MIDI";

    /// <summary>And the port of its own that really does carry Mackie Control.</summary>
    private const string LabMcu = "Minilab3 MCU/HUI";

    /// <summary>A KeyLab mkII's DAW port, which carries whichever protocol its menu chose.</summary>
    private const string KeyDaw = "KeyLab mkII 49 DAW";

    /// <summary>Its other port, which carries plain notes and controllers.</summary>
    private const string KeyMain = "KeyLab mkII 49 MIDI";

    /// <summary>A controller nobody has written a file for.</summary>
    private const string Nobodys = "Some Other Box Port 1";

    /// <summary>Reads the folder again, so each test starts from the files as they are on disc.</summary>
    public ControlSurfaceTests() => _profiles.Reload();

    /// <summary>
    /// A surface nobody has described is still read, which is the promise the protocol is read on.
    /// </summary>
    /// <remarks>
    /// The whole argument for reading Mackie Control is that it needs no file: it says what every
    /// control on it is, so a desk works the moment it is plugged in. Gating that on a profile
    /// would take a working device away in order to fix one that never worked.
    /// </remarks>
    [Fact]
    public void A_device_with_no_file_is_read_as_a_surface() =>
        Assert.True(_profiles.SurfaceOn(Nobodys));

    /// <summary>A device whose file describes it and names no protocol is not read as one.</summary>
    [Fact]
    public void A_described_device_that_names_no_protocol_is_not_a_surface() =>
        Assert.False(_profiles.SurfaceOn(Korg));

    /// <summary>And the port matters, not just the device: only the one carrying it counts.</summary>
    [Theory]
    [InlineData(LabMcu, true)]
    [InlineData(LabMain, false)]
    [InlineData(KeyDaw, true)]
    [InlineData(KeyMain, false)]
    public void A_surface_is_read_on_the_port_its_file_names(string port, bool speaks) =>
        Assert.Equal(speaks, _profiles.SurfaceOn(port));

    /// <summary>
    /// The knob collision itself: CC 16 off a nanoKONTROL2 moves nothing.
    /// </summary>
    /// <remarks>
    /// The one that matters, because the numbers are identical and nothing in the message tells
    /// them apart. Fed the very same byte off a port with no file, it moves a pan, which is what
    /// says the router still works and the gate is what changed.
    /// </remarks>
    [Fact]
    public void A_plain_controller_is_not_read_as_a_v_pot()
    {
        var moved = new Moves();
        var router = new MidiMackieRouter(moved, () => 8, null, _profiles);

        router.Handle(Knob(Korg));

        Assert.Empty(moved.Written);

        router.Handle(Knob(Nobodys));

        Assert.NotEmpty(moved.Written);
    }

    /// <summary>Turning V-pot one: continuous controller 16, one notch, on a given port.</summary>
    private static MidiMessage Knob(string device) => new()
    {
        Device = device,
        Type = MidiMessageType.ControlChange,
        Channel = 1,
        Value = 0x10,
        Data = 1
    };

    /// <summary>A nanoKONTROL2's buttons are momentary, and its file now says so.</summary>
    [Fact]
    public void A_korg_button_is_momentary_and_its_knob_is_not()
    {
        Assert.True(_profiles.Momentary(Korg, 1, 48));
        Assert.False(_profiles.Momentary(Korg, 1, 16));
    }

    /// <summary>A device with no file claims nothing, so nothing already working changes.</summary>
    [Fact]
    public void Nothing_is_claimed_about_a_button_nobody_described() =>
        Assert.False(_profiles.Momentary(Nobodys, 1, 48));

    /// <summary>
    /// A momentary button pointed at a mute flips it on the press and ignores the release.
    /// </summary>
    /// <remarks>
    /// The fault as it was reported: the mute followed the value, so a track was muted for
    /// exactly as long as a thumb was on the button and unmuted itself on the way up. Written as
    /// two messages because a momentary button is two messages, and the second one is the whole
    /// point: the release has to do nothing at all.
    ///
    /// Pressed twice here, so it is a flip rather than a write of one particular state: the
    /// second press has to put it back.
    /// </remarks>
    [Fact]
    public void A_momentary_button_flips_a_mute_rather_than_holding_it()
    {
        var strip = new Switched();
        var link = Mute();
        var router = new MidiControlRouter(() => new[] { link }, strip, profiles: _profiles);

        router.Handle(Press(Korg, 48, 127));
        Assert.True(strip.Muted);

        router.Handle(Press(Korg, 48, 0));
        Assert.True(strip.Muted);

        router.Handle(Press(Korg, 48, 127));
        Assert.False(strip.Muted);
    }

    /// <summary>
    /// And a button nobody has described still follows the value, which is what it always did.
    /// </summary>
    /// <remarks>
    /// The counterpart every rule here has. A latching button reports its own state, so
    /// following it is right, and there is nothing on the wire to tell the two apart: without a
    /// file saying momentary, nothing may be assumed.
    ///
    /// The first three messages are the ones the sensing spends working out what kind of control
    /// this is, and they are spent rather than obeyed, so the assertions come after them. That is
    /// deliberate everywhere and worth showing here: a device with no file pays thirty
    /// milliseconds and a described one does not, which is most of what a file buys.
    /// </remarks>
    [Fact]
    public void An_undescribed_button_still_follows_its_value()
    {
        var strip = new Switched();
        var link = Mute();
        link.Device = Nobodys;

        var router = new MidiControlRouter(() => new[] { link }, strip, profiles: _profiles);

        router.Handle(Press(Nobodys, 48, 127));
        router.Handle(Press(Nobodys, 48, 0));
        router.Handle(Press(Nobodys, 48, 127));

        router.Handle(Press(Nobodys, 48, 127));

        Assert.True(strip.Muted);

        router.Handle(Press(Nobodys, 48, 0));

        Assert.False(strip.Muted);
    }

    /// <summary>A link off that device's mute button, pointed at the mute of the track in front.</summary>
    private static ControlMapping Mute() => new()
    {
        Device = Korg,
        Channel = 1,
        Cc = 48,
        Kind = ControlKind.Mix,
        Mix = MixControl.Mute,
        Name = "Mute"
    };

    /// <summary>One half of a press: the button's controller, at the value it sent.</summary>
    /// <remarks>
    /// <c>IsOn</c> is set here rather than worked out, because the real thing sets it on the way
    /// off the wire and the transport router begins by asking for it. A helper that left it
    /// false builds a message no port ever sends, and the tests then say the router ignores
    /// everything, which it does, correctly, to a message that never happened.
    /// </remarks>
    private static MidiMessage Press(string device, int cc, int data) => new()
    {
        Device = device,
        Type = MidiMessageType.ControlChange,
        Channel = 1,
        Value = cc,
        Data = data,
        IsOn = data > 0
    };

    /// <summary>One mute, and nothing else, so what a button did to it can be read back.</summary>
    private sealed class Switched : IControlTargets
    {
        /// <summary>Whether the track is muted.</summary>
        public bool Muted { get; private set; }

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping) => new Flag(this);

        /// <inheritdoc/>
        public IReadOnlyList<ControlChoice> On(int track) => System.Array.Empty<ControlChoice>();

        /// <summary>The mute itself: a switch, so a momentary button flips it.</summary>
        private sealed class Flag(Switched strip) : IControlTarget
        {
            /// <inheritdoc/>
            public string Name => "Mute";

            /// <inheritdoc/>
            public double Min => 0;

            /// <inheritdoc/>
            public double Max => 1;

            /// <inheritdoc/>
            public double Value => strip.Muted ? 1 : 0;

            /// <inheritdoc/>
            public bool Switch => true;

            /// <inheritdoc/>
            public void Set(double value) => strip.Muted = value >= 0.5;
        }
    }

    /// <summary>
    /// A button pointed at the transport fires on every press, not on the first one only.
    /// </summary>
    /// <remarks>
    /// It fired about one press in three, which is worse than never working because it reads as
    /// a loose cable. Parking was the cause: a press writes the target's maximum, so the hand was
    /// parked upward, and a release read through the wrap unwinding is a step upward too, which
    /// is the same direction and therefore still parked. Parking is a rule about a control that
    /// reports a position driving a value into an end, and a press has neither a position nor an
    /// in between, so it is asked after the press branches now rather than before them.
    ///
    /// Three presses and their releases, and the count is the whole assertion: the releases must
    /// contribute nothing and every press must contribute one.
    /// </remarks>
    [Fact]
    public void A_transport_button_fires_on_every_press()
    {
        var fired = new Presses();

        var link = new ControlMapping
        {
            Device = Nobodys,
            Channel = 1,
            Cc = 20,
            Kind = ControlKind.Transport,
            Scope = ControlScope.Fixed,
            Transport = TransportKey.Play,
            Name = "Play"
        };

        var router = new MidiControlRouter(() => new[] { link }, fired, profiles: _profiles);

        for (int at = 0; at < 3; at++)
        {
            router.Handle(Press(Nobodys, 20, 127));
            router.Handle(Press(Nobodys, 20, 0));
        }

        Assert.Equal(3, fired.Count);
    }

    /// <summary>One transport key, counting what reached it.</summary>
    private sealed class Presses : IControlTargets
    {
        /// <summary>How many presses landed.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping) => new Key(this);

        /// <inheritdoc/>
        public IReadOnlyList<ControlChoice> On(int track) => System.Array.Empty<ControlChoice>();

        /// <summary>Shaped as the real one is: nought to one, reading nought, and not a switch.</summary>
        private sealed class Key(Presses keys) : IControlTarget
        {
            /// <inheritdoc/>
            public string Name => "Play";

            /// <inheritdoc/>
            public double Min => 0;

            /// <inheritdoc/>
            public double Max => 1;

            /// <inheritdoc/>
            public double Value => 0;

            /// <inheritdoc/>
            public void Set(double value) => keys.Count++;
        }
    }

    /// <summary>
    /// A controller whose file names its transport buttons works from the tick alone.
    /// </summary>
    /// <remarks>
    /// The inconsistency this fixes, stated as a test. A MiniLab 3 and a KeyLab mkII speak one
    /// of the three protocols, so ticking Transport in SETTINGS made their play buttons work
    /// with nothing pointed anywhere. A nanoKONTROL2's play button is plain controller 41 like
    /// its mute buttons, so the same tick did nothing whatever, and no amount of looking at
    /// either device would have told anybody why.
    ///
    /// The release must do nothing, the same as for the other dialects: acting on both halves
    /// would stop what the press had just started.
    /// </remarks>
    [Fact]
    public void A_described_transport_button_works_from_the_tick_alone()
    {
        var keys = new Keys();
        var router = new MidiTransportRouter(keys, _profiles);

        router.Handle(Press(Korg, 41, 127));
        router.Handle(Press(Korg, 41, 0));

        router.Handle(Press(Korg, 42, 127));
        router.Handle(Press(Korg, 45, 127));

        Assert.Equal("play stop record", string.Join(" ", keys.Asked));
    }

    /// <summary>A button its file does not call a transport button still does nothing.</summary>
    /// <remarks>
    /// Its mute and solo buttons are the ones that matter: they are the same kind of message on
    /// the same port, and a strip button reaching the transport would be far worse than a
    /// transport button reaching nothing. Its sliders are here for the same reason.
    /// </remarks>
    [Fact]
    public void Only_the_buttons_a_file_names_reach_the_transport()
    {
        var keys = new Keys();
        var router = new MidiTransportRouter(keys, _profiles);

        router.Handle(Press(Korg, 48, 127));
        router.Handle(Press(Korg, 32, 127));
        router.Handle(Press(Korg, 0, 127));
        router.Handle(Press(Korg, 16, 127));

        Assert.Empty(keys.Asked);
    }

    /// <summary>And its CYCLE key turns looping on or off, like every other cycle key.</summary>
    [Fact]
    public void A_described_cycle_key_reaches_the_loop()
    {
        var keys = new Keys();
        var router = new MidiTransportRouter(keys, _profiles);

        router.Handle(Press(Korg, 46, 127));
        router.Handle(Press(Korg, 46, 0));

        Assert.Equal(new[] { "cycle" }, keys.Asked);
    }

    /// <summary>And a device with no file has no transport buttons, as it had none before.</summary>
    [Fact]
    public void A_device_with_no_file_still_needs_one_of_the_protocols()
    {
        var keys = new Keys();
        var router = new MidiTransportRouter(keys, _profiles);

        router.Handle(Press(Nobodys, 41, 127));

        Assert.Empty(keys.Asked);

        router.Handle(Press(Nobodys, 107, 127));

        Assert.Equal("play", string.Join(" ", keys.Asked));
    }

    /// <summary>What the transport was asked for, in order.</summary>
    private sealed class Keys : ITransportKeys
    {
        /// <summary>Every word it was told, so a test can say it was told nothing.</summary>
        public List<string> Asked { get; } = new();

        /// <inheritdoc/>
        public void Play() => Asked.Add("play");

        /// <inheritdoc/>
        public void Stop() => Asked.Add("stop");

        /// <inheritdoc/>
        public void Record() => Asked.Add("record");

        /// <inheritdoc/>
        public void Loop() => Asked.Add("cycle");
    }

    /// <summary>The mixer, as far as the Mackie router can see it.</summary>
    private sealed class Moves : IControlTargets
    {
        /// <summary>Every write that reached a target, so a test can say nothing reached one.</summary>
        public List<string> Written { get; } = new();

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping)
        {
            if (mapping is null) return null;

            return new Wrote(this, mapping);
        }

        /// <inheritdoc/>
        public IReadOnlyList<ControlChoice> On(int track) => System.Array.Empty<ControlChoice>();

        /// <summary>A target that only remembers it was written to.</summary>
        private sealed class Wrote(Moves desk, ControlMapping mapping) : IControlTarget
        {
            /// <inheritdoc/>
            public string Name => mapping.Name;

            /// <inheritdoc/>
            public double Min => -1;

            /// <inheritdoc/>
            public double Max => 1;

            /// <inheritdoc/>
            public double Value => 0;

            /// <inheritdoc/>
            public void Set(double value) => desk.Written.Add(mapping.Mix + " " + value);
        }
    }
}
