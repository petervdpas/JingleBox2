using System.Collections.Generic;
using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Music;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two wheels beside a keyboard, from the bytes to a lean and an amount.
/// </summary>
/// <remarks>
/// The same shape as <see cref="NotePathTests"/> and for the same reason: the wire and the
/// router are both public, so the whole of it can be played with nothing plugged in.
///
/// What is worth being exact about here is the resting position. A wheel is a position rather
/// than an event, so it is right for as long as it is held and wrong for as long as it is held,
/// and a wheel at rest reading anything but nought is every note on the track sitting
/// permanently off its own pitch with nothing on the screen to say so.
/// </remarks>
public class WheelWireTests
{
    /// <summary>A pitch wheel sitting where it sits when nobody is touching it.</summary>
    /// <remarks>
    /// Exactly nought, not nearly. Fourteen bits put 8192 steps below the middle and 8191 above,
    /// so one divisor cannot give both an exact end and an exact middle, and it is the middle
    /// that has to be exact.
    /// </remarks>
    [Fact]
    public void A_pitch_wheel_at_rest_leans_nowhere()
    {
        Assert.Equal(0, new MidiWheelInput().LeanFor(8192));
    }

    /// <summary>And its two ends reach all the way, each of them.</summary>
    [Fact]
    public void Both_ends_of_a_pitch_wheel_are_reached_exactly()
    {
        var wire = new MidiWheelInput();

        Assert.Equal(-1, wire.LeanFor(0));
        Assert.Equal(1, wire.LeanFor(16383));
    }

    /// <summary>
    /// Halfway up really is halfway, to within the asymmetry fourteen bits force on it.
    /// </summary>
    /// <remarks>
    /// The upper half has one step fewer than the lower, so a point halfway up it lands a
    /// sixteen thousandth over. That is a hundredth of a cent at a two semitone range and is the
    /// price of both ends and the middle being exact, which are the three places anybody can
    /// hear.
    /// </remarks>
    [Fact]
    public void Halfway_up_is_half_a_lean()
    {
        Assert.Equal(0.5, new MidiWheelInput().LeanFor(8192 + 4096), 3);
        Assert.Equal(-0.5, new MidiWheelInput().LeanFor(8192 - 4096), 6);
    }

    /// <summary>
    /// A value off the end of the scale is the end of the scale.
    /// </summary>
    /// <remarks>
    /// Clamped rather than refused, unlike a note number: a bend is a position and a position
    /// past its own end has nothing else it could mean, where a note past the top of a pattern
    /// would pile every key beyond the edge onto one pitch.
    /// </remarks>
    [Fact]
    public void A_pitch_wheel_past_its_own_ends_is_held_at_them()
    {
        var wire = new MidiWheelInput();

        Assert.Equal(-1, wire.LeanFor(-500));
        Assert.Equal(1, wire.LeanFor(99999));
    }

    /// <summary>The modulation wheel runs up from nothing, and nothing is nothing.</summary>
    [Fact]
    public void A_modulation_wheel_down_asks_for_nothing_at_all()
    {
        var wire = new MidiWheelInput();

        Assert.Equal(0, wire.AmountFor(0));
        Assert.Equal(1, wire.AmountFor(127));
        Assert.Equal(0.5, wire.AmountFor(64), 1);
    }

    /// <summary>
    /// The fourteen bits arrive least significant first, which is the one way to read them.
    /// </summary>
    /// <remarks>
    /// Read the other way round a wheel pushed gently to the top reads as a wheel slammed to the
    /// bottom. It is the kind of thing that sounds like a bug in the engine rather than in the
    /// reading, so it is pinned off the raw bytes rather than trusted.
    /// </remarks>
    [Fact]
    public void A_bend_is_read_off_the_wire_low_byte_first()
    {
        Assert.Equal(new[] { "bend 0" }, Play(new byte[] { 0xE0, 0x00, 0x40 }));
        Assert.Equal(new[] { "bend -1" }, Play(new byte[] { 0xE0, 0x00, 0x00 }));
        Assert.Equal(new[] { "bend 1" }, Play(new byte[] { 0xE0, 0x7F, 0x7F }));
    }

    /// <summary>Controller one is the modulation wheel, and it is the only controller that is.</summary>
    /// <remarks>
    /// The number is the specification's, which is what lets a keyboard nobody has written a
    /// file for work the moment it is plugged in. Controller two is the strip beside it on some
    /// devices and is not a wheel: read as one it would bring in vibrato from a control nobody
    /// pointed at anything.
    /// </remarks>
    [Fact]
    public void Controller_one_is_the_wheel_and_nothing_else_is()
    {
        Assert.Equal(new[] { "modulate 1" }, Play(new byte[] { 0xB0, 1, 127 }));
        Assert.Empty(Play(new byte[] { 0xB0, 2, 127 }));
        Assert.Empty(Play(new byte[] { 0xB0, 74, 127 }));
    }

    /// <summary>A key is not a wheel, which is what keeps the two routers out of each other.</summary>
    [Fact]
    public void A_note_is_no_part_of_this()
    {
        Assert.Empty(Play(new byte[] { 0x90, 60, 100 }));
    }

    /// <summary>
    /// A keyboard nobody has written a file for still has its wheels.
    /// </summary>
    /// <remarks>
    /// The one thing here that had to be got wrong once to be seen. Standing the wheels down on
    /// a port that might be a Mackie surface reads as an obvious guard, and the question a
    /// profile answers about surfaces defaults to <b>yes</b> for a device with no file: asked,
    /// it silenced the wheels on every keyboard this application has never heard of, which is
    /// nearly all of them and is the whole case any of this exists for.
    /// </remarks>
    [Fact]
    public void A_keyboard_nobody_has_described_still_has_its_wheels()
    {
        Assert.False(new ControllerProfiles().Knows("nameless keyboard"));

        Assert.Equal(new[] { "bend 1" }, Play(new byte[] { 0xE0, 0x7F, 0x7F }));
        Assert.Equal(new[] { "modulate 1" }, Play(new byte[] { 0xB0, 1, 127 }));
    }

    /// <summary>
    /// A controller's own file beats the number, which is what keeps a slider a slider.
    /// </summary>
    /// <remarks>
    /// The one thing this rule can get wrong, and it was got wrong once: a nanoKONTROL2's second
    /// slider is controller one. Read as a wheel it would bend whatever was playing every time
    /// somebody moved track two's level, and its own file has said Slider 2 all along.
    /// </remarks>
    [Fact]
    public void A_file_that_names_controller_one_something_else_keeps_it()
    {
        var profiles = new ControllerProfiles();

        Assert.False(Jobs(profiles).Turns(Modulation("nanoKONTROL2 _ CTRL", 1)));
        Assert.True(Jobs(new Undescribed()).Turns(Modulation("nanoKONTROL2 _ CTRL", 1)));
    }

    /// <summary>And a device whose file calls it a wheel is one, which is what the three here say.</summary>
    [Fact]
    public void A_file_that_names_it_a_wheel_says_so()
    {
        var profiles = new ControllerProfiles();

        Assert.True(Jobs(profiles).Turns(Modulation("KeyLab mkII 49 MIDI 1", 1)));
    }

    /// <summary>
    /// A modulation strip is a modulation wheel, whatever its own file calls it.
    /// </summary>
    /// <remarks>
    /// The narrower test was wrong and this is what caught it. Every profile written before the
    /// word wheel existed calls that control something else, and a MiniLab 3 and a KeyStep Pro
    /// both say `strip`, which is what their modulation wheel physically is: asked whether the
    /// file says wheel, both had their wheels switched off by describing themselves correctly.
    /// And a profile is copied into the application folder once and never updated, so correcting
    /// the shipped file would not have reached anybody who already had it.
    /// </remarks>
    [Fact]
    public void A_modulation_strip_is_still_a_wheel()
    {
        Assert.True(Jobs(new Strip()).Turns(Modulation("Minilab3 MIDI", 1)));
        Assert.True(Jobs(new ControllerProfiles()).Turns(Modulation("Minilab3 MIDI", 1)));
    }

    /// <summary>
    /// And the default layout never claims the wheel, whatever a file says it is.
    /// </summary>
    /// <remarks>
    /// This is the fault the whole arrangement exists to end. A wheel reports a position, so
    /// watching it files it with the faders, and a fader with no file is pointed at the first
    /// track's level: moving the wheel turned a track down. Refused on the number, since that is
    /// what makes it true of a keyboard nobody has described.
    /// </remarks>
    [Fact]
    public void The_layout_never_points_the_wheel_at_a_track()
    {
        var layout = new DefaultLayout();

        for (int at = 0; at < 6; at++)
        {
            Assert.Null(layout.For(new MidiMessage
            {
                Device = "keyboard",
                Type = MidiMessageType.ControlChange,
                Channel = 1,
                Value = 1,
                Data = at * 20
            }));
        }
    }

    /// <summary>The rule over those profiles, which is what decides whether a control is a wheel.</summary>
    private static IControlJobs Jobs(IControllerProfiles profiles) => new ControlJobs(profiles);

    /// <summary>A modulation wheel message from that device.</summary>
    private static MidiMessage Modulation(string device, int value) => new()
    {
        Device = device, Type = MidiMessageType.ControlChange, Channel = 1, Value = 1, Data = value
    };

    /// <summary>Plays those messages through the wire and the router, and says what was heard.</summary>
    private static IReadOnlyList<string> Play(params byte[][] messages)
    {
        var service = new MidiService();
        var heard = new Wheels();
        var router = new MidiWheelRouter(heard);

        foreach (var bytes in messages)
        {
            var message = service.Read("keyboard", bytes, 0, bytes.Length);
            if (message != null) router.Handle(message);
        }

        return heard.Said;
    }

    /// <summary>A machine where controller one is described the way it was before wheels existed.</summary>
    /// <remarks>
    /// Answered directly rather than by editing a shipped file, since the point is that a
    /// profile already on somebody's disc says this and cannot be corrected: see
    /// <see cref="A_modulation_strip_is_still_a_wheel"/>.
    /// </remarks>
    private sealed class Strip : IControllerProfiles
    {
        /// <summary>The real one, for everything this is not about.</summary>
        private readonly ControllerProfiles _real = new();

        /// <inheritdoc/>
        public Controllers.ControllerControl? Control(string? device, int channel, int cc) =>
            new() { Name = "Mod strip", Cc = 1, Kind = "strip" };

        /// <inheritdoc/>
        public bool SurfaceOn(string? device) => _real.SurfaceOn(device);

        /// <inheritdoc/>
        public string ScreenOn(string? device) => _real.ScreenOn(device);

        /// <inheritdoc/>
        public bool ScreenWakes(string? device) => _real.ScreenWakes(device);

        /// <inheritdoc/>
        public bool Momentary(string? device, int channel, int cc) => _real.Momentary(device, channel, cc);

        /// <inheritdoc/>
        public TransportKey? TransportOn(string? device, int channel, int cc) =>
            _real.TransportOn(device, channel, cc);

        /// <inheritdoc/>
        public void Reload() => _real.Reload();

        /// <inheritdoc/>
        public Controllers.ControllerProfile? For(string? device) => _real.For(device);

        /// <inheritdoc/>
        public string Called(string? device) => _real.Called(device);

        /// <inheritdoc/>
        public bool Knows(string? device) => _real.Knows(device);

        /// <inheritdoc/>
        public void Saw(string? device, int channel, int cc) => _real.Saw(device, channel, cc);

        /// <inheritdoc/>
        public string ProgramOn(string? device) => _real.ProgramOn(device);

        /// <inheritdoc/>
        public string Named(string? device, int channel, int cc) => _real.Named(device, channel, cc);

        /// <inheritdoc/>
        public string PortIs(string? device) => _real.PortIs(device);

        /// <inheritdoc/>
        public bool PortTakes(string? device, MidiPortRole role) => _real.PortTakes(device, role);

        /// <inheritdoc/>
        public ControlPickup? Pickup(string? device, int channel, int cc) => _real.Pickup(device, channel, cc);
    }

    /// <summary>A machine that has never heard of any controller, for the number's own rule.</summary>
    private sealed class Undescribed : IControllerProfiles
    {
        /// <summary>The real one, for everything this is not about.</summary>
        private readonly ControllerProfiles _real = new();

        /// <inheritdoc/>
        public Controllers.ControllerControl? Control(string? device, int channel, int cc) => null;

        /// <inheritdoc/>
        public bool SurfaceOn(string? device) => _real.SurfaceOn(device);

        /// <inheritdoc/>
        public string ScreenOn(string? device) => _real.ScreenOn(device);

        /// <inheritdoc/>
        public bool ScreenWakes(string? device) => _real.ScreenWakes(device);

        /// <inheritdoc/>
        public bool Momentary(string? device, int channel, int cc) => _real.Momentary(device, channel, cc);

        /// <inheritdoc/>
        public TransportKey? TransportOn(string? device, int channel, int cc) =>
            _real.TransportOn(device, channel, cc);

        /// <inheritdoc/>
        public void Reload() => _real.Reload();

        /// <inheritdoc/>
        public Controllers.ControllerProfile? For(string? device) => _real.For(device);

        /// <inheritdoc/>
        public string Called(string? device) => _real.Called(device);

        /// <inheritdoc/>
        public bool Knows(string? device) => _real.Knows(device);

        /// <inheritdoc/>
        public void Saw(string? device, int channel, int cc) => _real.Saw(device, channel, cc);

        /// <inheritdoc/>
        public string ProgramOn(string? device) => _real.ProgramOn(device);

        /// <inheritdoc/>
        public string Named(string? device, int channel, int cc) => _real.Named(device, channel, cc);

        /// <inheritdoc/>
        public string PortIs(string? device) => _real.PortIs(device);

        /// <inheritdoc/>
        public bool PortTakes(string? device, MidiPortRole role) => _real.PortTakes(device, role);

        /// <inheritdoc/>
        public ControlPickup? Pickup(string? device, int channel, int cc) => _real.Pickup(device, channel, cc);
    }

    /// <summary>Somewhere for the wheels to land, in the order they landed.</summary>
    private sealed class Wheels : IPlays
    {
        /// <summary>Each move, in the order it arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void Press(int track, Note note, int volume)
        {
        }

        /// <inheritdoc/>
        public void Let(int track, Note note)
        {
        }

        /// <inheritdoc/>
        public void Bend(int track, double lean) => Said.Add("bend " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(int track, double amount) =>
            Said.Add("modulate " + amount.ToString("0.###"));
    }
}
