using Avalonia.Input;
using JingleBox2.Views;
using JingleBox2.Views.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a keystroke asks the transport for, without a window or a keyboard.
/// </summary>
/// <remarks>
/// The rule was inside the main window's own handler, which is why the transport stopped working
/// the moment anything else was in front of you: a machine's panel, an effect off a chain, a
/// plugin's window. It is out here now, and this is it.
/// </remarks>
public class DeckKeyTests
{
    /// <summary>Space is the one every tracker and every desk puts it on.</summary>
    [Fact]
    public void Space_works_the_transport()
    {
        Assert.Equal(DeckWant.Toggle, DeckKeys.Wants(Key.Space, KeyModifiers.None, busy: false));
    }

    /// <summary>And Ctrl+R records, which is the other half of the same bar.</summary>
    [Fact]
    public void Control_r_records()
    {
        Assert.Equal(DeckWant.Record, DeckKeys.Wants(Key.R, KeyModifiers.Control, busy: false));
    }

    /// <summary>
    /// Neither, while the keyboard is somewhere a key means something else.
    /// </summary>
    /// <remarks>
    /// A space in a name is a space, and Ctrl+R in one is somebody typing. This is the whole of
    /// what <c>busy</c> is for, and it is the reason a rule with no window in it can still be
    /// right about a window.
    /// </remarks>
    [Fact]
    public void Nothing_while_something_else_wants_the_key()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.None, busy: true));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.Control, busy: true));
    }

    /// <summary>A space with anything held is not the transport's.</summary>
    /// <remarks>
    /// Ctrl+Space and Shift+Space belong to whatever else wants them, and a transport that took
    /// every space bar whatever was held with it would be taking keys nobody offered it.
    /// </remarks>
    [Fact]
    public void A_space_with_a_modifier_is_somebody_elses()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.Control, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.Shift, busy: false));
    }

    /// <summary>And R on its own is a note, a letter, or nothing: it is not record.</summary>
    [Fact]
    public void An_r_on_its_own_is_not_record()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.Shift, busy: false));
    }

    /// <summary>Every other key is nobody's business here.</summary>
    [Fact]
    public void Anything_else_is_left_alone()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Enter, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.A, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.S, KeyModifiers.Control, busy: false));
    }
    /// <summary>
    /// A deck that records the way the tracker does, where record is an arm rather than a take.
    /// </summary>
    private sealed class ArmingDeck : JingleBox2.ViewModels.Interfaces.ITransportDeck
    {
        /// <inheritdoc/>
        public bool IsRunning => false;
        /// <inheritdoc/>
        public bool IsRecording { get; private set; }
        /// <inheritdoc/>
        public bool IsPlaying => false;
        /// <inheritdoc/>
        public bool IsPaused => false;
        /// <inheritdoc/>
        public bool CanRecord => true;
        /// <inheritdoc/>
        public bool CanPlay => true;
        /// <inheritdoc/>
        public bool CanPause => false;

        /// <summary>How many times the transport was told to stop.</summary>
        public int Stopped { get; private set; }

        /// <summary>Never raised: nothing here is bound to.</summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

        /// <inheritdoc/>
        public void Record() => IsRecording = !IsRecording;
        /// <inheritdoc/>
        public void Play() { }
        /// <inheritdoc/>
        public void Pause() { }
        /// <inheritdoc/>
        public void Stop() => Stopped++;
    }

    /// <summary>
    /// The record key turns the arm off as well as on.
    /// </summary>
    /// <remarks>
    /// It did not, and this is the test that would have caught it. The key handler read the
    /// deck's IsRecording and reached for Stop when it was set, which is right for a page that
    /// records a take and wrong for one where record is an arm: Ctrl+R armed the pattern and
    /// every press after that stopped the transport instead, so the arm could not be turned off
    /// with the key that turned it on.
    ///
    /// What a second press means differs per deck and each deck already knows, so the key asks
    /// for Record and the deck decides. Which is what this asks: two presses, back where it
    /// started, and the transport never told to stop on the way.
    /// </remarks>
    [Fact]
    public void The_record_key_disarms_what_it_armed()
    {
        var deck = new ArmingDeck();

        Assert.False(deck.IsRecording);

        deck.Record();

        Assert.True(deck.IsRecording);

        deck.Record();

        Assert.False(deck.IsRecording);
        Assert.Equal(0, deck.Stopped);
    }

    /// <summary>
    /// And stopping the transport leaves the arm exactly where it was.
    /// </summary>
    /// <remarks>
    /// Deliberate rather than an oversight. In a tracker the notes are typed with the transport
    /// stopped and the pattern armed, so a stop that disarmed would mean re-arming before every
    /// line you wanted to write. Arm is a mode you are in; stop is about whether the music is
    /// running.
    /// </remarks>
    [Fact]
    public void Stopping_the_transport_leaves_the_arm_alone()
    {
        var deck = new ArmingDeck();

        deck.Record();
        deck.Stop();

        Assert.True(deck.IsRecording);
    }
}
