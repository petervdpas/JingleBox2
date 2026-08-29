using CommunityToolkit.Mvvm.Input;
using JingleBox2.Midi;
using JingleBox2.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// What a panel's keyboard shows lit, through the interface a keyboard is driven by.
/// </summary>
/// <remarks>
/// What it lights is a monitor of the notes going past, which is why a key on the hardware
/// lights it: that key never touches a panel, it goes to whoever the notes are being played on.
/// A panel that kept its own record of the presses it had heard showed nothing for the one
/// keyboard people actually play.
///
/// Against <see cref="DesignerKeys"/> itself rather than against the monitor alone, because the
/// wiring is half the rule: which presses also sound a note, which do not, and whether a key let
/// go of goes dark while what it started is still ringing.
/// </remarks>
public class MachineKeysTests
{
    /// <summary>A key on the drawn keyboard lights and sounds, and sounds exactly once.</summary>
    /// <remarks>
    /// A panel sounds its own keys, since the press never went anywhere else to be played.
    /// </remarks>
    [Fact]
    public void A_key_pressed_here_lights_and_sounds_once()
    {
        var (keys, designer) = Keyboard();

        keys.Play(60);

        Assert.Equal(new[] { 60 }, Lit(keys));
        Assert.Equal(1, designer.Played);
    }

    /// <summary>And letting go puts the light out and lets the note go, once each.</summary>
    [Fact]
    public void And_goes_dark_when_it_is_let_go_of()
    {
        var (keys, designer) = Keyboard();

        keys.Play(60);
        keys.Let(60);

        Assert.Empty(Lit(keys));
        Assert.Equal(1, designer.Let);
    }

    /// <summary>
    /// The hardware: seen going past, and not played again by the panel that drew it.
    /// </summary>
    /// <remarks>
    /// A key on a MIDI keyboard has been sounded by whoever the notes are going to by the time
    /// any panel hears about it. Playing it here as well would sound every note twice.
    /// </remarks>
    [Fact]
    public void A_key_on_the_hardware_lights_without_being_sounded_again()
    {
        var (keys, designer) = Keyboard();

        designer.Monitor.TriggerNote(new Note(60), 100);

        Assert.Equal(new[] { 60 }, Lit(keys));
        Assert.Equal(0, designer.Played);

        designer.Monitor.ReleaseNote(new Note(60));

        Assert.Empty(Lit(keys));
        Assert.Equal(0, designer.Let);
    }

    /// <summary>
    /// And a panel opened while a key is already down shows it at once.
    /// </summary>
    /// <remarks>
    /// Which is the difference between reading a monitor and keeping a record of what you
    /// yourself heard: a window opened mid-chord would have started blind.
    /// </remarks>
    [Fact]
    public void A_keyboard_shows_what_is_already_down_when_it_appears()
    {
        var designer = new Designer();

        designer.Monitor.TriggerNote(new Note(60), 100);

        Assert.Equal(new[] { 60 }, Lit(designer.MachineKeys));
    }

    /// <summary>
    /// Two keyboards show the same keys, because there is one answer and they both read it.
    /// </summary>
    /// <remarks>
    /// A panel on the rack and an instrument's own window are open at once often enough, and a
    /// key is down or it is not.
    /// </remarks>
    [Fact]
    public void Every_keyboard_watching_shows_the_same_keys()
    {
        var monitor = new MidiMonitor();

        var one = new Designer(monitor);
        var two = new Designer(monitor);

        monitor.TriggerNote(new Note(64), 100);

        Assert.Equal(new[] { 64 }, Lit(one.MachineKeys));
        Assert.Equal(new[] { 64 }, Lit(two.MachineKeys));
    }

    /// <summary>
    /// A keyboard taken off the wall stops being told, and does not hold the monitor open.
    /// </summary>
    /// <remarks>
    /// The monitor outlives every panel. An instrument's window closed with nothing taken off it
    /// would go on being told about keys for the rest of the session.
    /// </remarks>
    [Fact]
    public void A_keyboard_put_down_stops_listening()
    {
        var designer = new Designer();
        var keys = designer.MachineKeys;

        monitorPress(designer, 60);
        Assert.Equal(new[] { 60 }, Lit(keys));

        ((System.IDisposable)keys).Dispose();

        monitorPress(designer, 64);
        Assert.Equal(new[] { 60 }, Lit(keys));

        static void monitorPress(Designer designer, int semitone) =>
            designer.Monitor.TriggerNote(new Note(semitone), 100);
    }

    /// <summary>
    /// A sounding note is not a key. Nothing lights until something is pressed.
    /// </summary>
    /// <remarks>
    /// A keyboard shows keys, which are events with two halves, and what is sounding is a thing
    /// with a length. They are different questions and the pads answer the second one.
    /// </remarks>
    [Fact]
    public void A_sounding_note_lights_no_key()
    {
        var (keys, designer) = Keyboard();

        designer.Sounding.Struck(new Note(64), 4.0);

        Assert.Empty(Lit(keys));
    }

    /// <summary>
    /// And a key let go of goes dark at once, however long what it started rings on.
    /// </summary>
    /// <remarks>
    /// This is the lag the rule exists to avoid: a cymbal sounds for four seconds and the key
    /// that started it was down for a tenth of one.
    /// </remarks>
    [Fact]
    public void A_key_let_go_of_goes_dark_while_its_note_rings_on()
    {
        var (keys, designer) = Keyboard();

        keys.Play(60);
        designer.Sounding.Struck(new Note(60), 4.0);

        keys.Let(60);

        Assert.Empty(Lit(keys));
        Assert.Contains(60, designer.Sounding.Lit);
    }

    /// <summary>
    /// A key held down is not pressed again, and one press is one release.
    /// </summary>
    /// <remarks>
    /// Holding a letter on the computer keyboard repeats it for as long as it is held, and a
    /// machine retriggered forty times a second is not what anybody meant by leaning on a key.
    /// </remarks>
    [Fact]
    public void A_key_already_down_is_not_pressed_again()
    {
        var (keys, designer) = Keyboard();

        designer.Monitor.TriggerNote(new Note(60), 100);
        keys.Play(60);

        Assert.Equal(new[] { 60 }, Lit(keys));
        Assert.Equal(0, designer.Played);

        designer.Monitor.ReleaseNote(new Note(60));
        Assert.Empty(Lit(keys));
    }

    /// <summary>A release with no press in front of it is not a fault.</summary>
    /// <remarks>
    /// The mouse can leave a key it never landed on, and a device already holding a note when
    /// the program starts sends a release nobody remembers.
    /// </remarks>
    [Fact]
    public void A_key_that_was_never_down_can_be_let_go_of_safely()
    {
        var (keys, designer) = Keyboard();

        keys.Let(60);

        Assert.Empty(Lit(keys));
        Assert.Equal(0, designer.Let);
    }

    /// <summary>A chord: every key held is lit, and each goes out on its own.</summary>
    [Fact]
    public void Every_key_held_is_lit()
    {
        var (keys, designer) = Keyboard();

        designer.Monitor.TriggerNote(new Note(60), 100);
        designer.Monitor.TriggerNote(new Note(64), 100);
        designer.Monitor.TriggerNote(new Note(67), 100);

        Assert.Equal(new[] { 60, 64, 67 }, Lit(keys).OrderBy(one => one));

        designer.Monitor.ReleaseNote(new Note(64));

        Assert.Equal(new[] { 60, 67 }, Lit(keys).OrderBy(one => one));
    }

    /// <summary>
    /// The lit set is one collection for the life of the keyboard, written into rather than
    /// replaced.
    /// </summary>
    /// <remarks>
    /// Both keyboards watch it for changes, and a fresh list on every read is a list nothing can
    /// watch: the picture would go on showing whatever was lit when it was bound.
    /// </remarks>
    [Fact]
    public void The_lit_set_is_the_same_collection_throughout()
    {
        var (keys, designer) = Keyboard();

        var first = keys.Lit;

        designer.Monitor.TriggerNote(new Note(60), 100);
        designer.Monitor.ReleaseNote(new Note(60));

        Assert.Same(first, keys.Lit);
    }

    /// <summary>And it says so when it moves, since a drawn keyboard reads it on being told.</summary>
    [Fact]
    public void It_says_when_what_is_lit_moves()
    {
        var (keys, designer) = Keyboard();

        int said = 0;
        keys.Changed += (_, _) => said++;

        designer.Monitor.TriggerNote(new Note(60), 100);

        Assert.True(said > 0);
    }

    /// <summary>
    /// Letting go of a key that was never pressed here changes nothing.
    /// </summary>
    /// <remarks>
    /// Which is what arrives when a device was already holding a note as the program started,
    /// and after a panic. Letting go of a sound this keyboard did not start is not its business.
    /// </remarks>
    [Fact]
    public void Letting_go_of_a_key_this_keyboard_never_pressed_changes_nothing()
    {
        var (keys, designer) = Keyboard();

        designer.Sounding.Struck(new Note(60), 4.0);

        keys.Let(60);

        Assert.Empty(Lit(keys));
        Assert.Equal(0, designer.Let);
    }

    /// <summary>A key let go of twice is let go of once.</summary>
    [Fact]
    public void A_second_release_does_nothing()
    {
        var (keys, designer) = Keyboard();

        keys.Play(60);

        keys.Let(60);
        keys.Let(60);

        Assert.Empty(Lit(keys));
        Assert.Equal(1, designer.Let);
    }

    /// <summary>
    /// A hand on a key outlasts the silence, which is what a stop button leaves behind.
    /// </summary>
    /// <remarks>
    /// Everything sounding goes dark when the transport stops. A finger still on a key has not
    /// moved, and the key it is on is still down: the two have nothing to do with each other.
    /// </remarks>
    [Fact]
    public void Silence_does_not_lift_a_hand_off_a_key()
    {
        var (keys, designer) = Keyboard();

        designer.Monitor.TriggerNote(new Note(60), 100);
        designer.Sounding.Struck(new Note(60), 4.0);

        designer.Sounding.Silence();

        Assert.Equal(new[] { 60 }, Lit(keys));

        designer.Monitor.ReleaseNote(new Note(60));

        Assert.Empty(Lit(keys));
    }

    /// <summary>
    /// A note number outside any keyboard is still a number, and must not throw.
    /// </summary>
    /// <remarks>
    /// Nothing sends one on purpose. A codec written in Lua can, a controller file can name a
    /// nonsense note, and this is the last place between either of those and a drawn key.
    ///
    /// It is still shown, because a monitor shows what went past, and taken quietly: what
    /// cannot be drawn on a keyboard is simply not drawn.
    /// </remarks>
    [Fact]
    public void A_note_that_is_on_no_keyboard_is_taken_quietly()
    {
        var (keys, designer) = Keyboard();

        designer.Monitor.Pressed(-1);
        designer.Monitor.Pressed(500);

        Assert.Equal(new[] { -1, 500 }, Lit(keys).OrderBy(one => one));

        designer.Monitor.Released(-1);
        designer.Monitor.Released(500);

        Assert.Empty(Lit(keys));
    }

    /// <summary>A keyboard and the designer under it, which is the pair every test needs.</summary>
    private static (IMachineKeys Keys, Designer Designer) Keyboard()
    {
        var designer = new Designer();

        return (designer.MachineKeys, designer);
    }

    /// <summary>What the keyboard is showing lit, as note numbers.</summary>
    private static IEnumerable<int> Lit(IMachineKeys keys) => keys.Lit.Cast<int>();

    /// <summary>Enough of a designer to hang a keyboard on: what it played and heard.</summary>
    /// <remarks>
    /// Everything below the monitor, the sounding notes and the two counters is what
    /// <see cref="IInstrumentDesigner"/> asks for and no test looks at. A real designer owns a
    /// window; this one owns two numbers.
    /// </remarks>
    private sealed class Designer : IInstrumentDesigner
    {
        /// <summary>Made on first use, so two designers sharing a monitor still differ.</summary>
        private IMachineKeys? _keys;

        /// <summary>Takes a monitor to share, or keeps one of its own.</summary>
        public Designer(MidiMonitor? monitor = null) => Monitor = monitor ?? new MidiMonitor();

        /// <summary>The notes going past, which is where a keyboard's lights come from.</summary>
        public MidiMonitor Monitor { get; }

        public IMidiMonitor? MidiKeys => Monitor;

        public InstrumentEditorViewModel? Editor => null;

        public int Octave { get; set; } = 4;

        public int NoteTrigger => 0;

        public double ScopeCycles { get; set; }

        public double HoldSeconds => 0.4;

        public IRelayCommand TestCommand { get; } = new RelayCommand(() => { });

        /// <summary>What is ringing, which is a different question from what is held down.</summary>
        public SoundingNotes Sounding { get; } = new();

        /// <summary>The keyboard under test, built once and kept.</summary>
        public IMachineKeys MachineKeys => _keys ??= new DesignerKeys(this);

        public InstrumentPresets? Presets => null;

        public TrackLocationViewModel? Location => null;

        public bool HasLocation => false;

        public IMachineLocation? MachineLocation => null;

        /// <summary>How many notes the panel asked to be sounded.</summary>
        public int Played { get; private set; }

        /// <summary>How many it asked to be let go of.</summary>
        public int Let { get; private set; }

        /// <inheritdoc/>
        void IInstrumentDesigner.Play(Note note, int volume) => Played++;

        /// <inheritdoc/>
        void IInstrumentDesigner.Let(Note note) => Let++;
    }
}
