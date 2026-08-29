using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the host remembers a plugin is holding.
/// </summary>
/// <remarks>
/// A plugin cannot be asked what is sounding inside it, so the only thing a host has is what it
/// said, and every per-note ending is decided from that record. It is worth its own tests
/// because it is exactly the kind of bookkeeping that goes wrong quietly: a note forgotten here
/// is a note that hangs until the transport stops, and a note remembered twice is a chord that
/// will not let go of one of its own.
///
/// No plugin, no process and no sound card: it is a list and the rules for taking things out
/// of it.
/// </remarks>
public class HeldNotesTests
{
    /// <summary>Room for whatever a test lets go of at once.</summary>
    private static int[] Room() => new int[HeldNotes.Most];

    /// <summary>A note written down is a note being held.</summary>
    [Fact]
    public void A_pressed_note_is_held()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);

        Assert.Equal(1, held.Count);
        Assert.True(held.Holds(60));
        Assert.False(held.Holds(61));
    }

    /// <summary>A chord is several, and all of them are held.</summary>
    [Fact]
    public void A_chord_is_held_whole()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);
        held.Press(64);
        held.Press(67);

        Assert.Equal(3, held.Count);
        Assert.True(held.Holds(64));
    }

    /// <summary>The same note pressed again is one note and not two.</summary>
    [Fact]
    public void The_same_note_pressed_twice_is_held_once()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);
        held.Press(60);

        Assert.Equal(1, held.Count);
    }

    /// <summary>Letting one go leaves the rest of the chord alone.</summary>
    [Fact]
    public void Letting_one_note_go_keeps_the_others()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);
        held.Press(64);

        Assert.True(held.Let(60));
        Assert.False(held.Holds(60));
        Assert.True(held.Holds(64));
    }

    /// <summary>A note nobody is holding cannot be let go of, and says so.</summary>
    /// <remarks>
    /// That answer is what stops a note off being passed on for a note this side never started,
    /// which would end one the plugin is holding for somebody else.
    /// </remarks>
    [Fact]
    public void Letting_go_of_a_note_nobody_holds_says_no()
    {
        IHeldNotes held = new HeldNotes();

        Assert.False(held.Let(60));
    }

    /// <summary>Letting them all go hands every one of them back, and empties the record.</summary>
    [Fact]
    public void Letting_all_go_hands_back_every_note()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);
        held.Press(64);
        held.Press(67);

        var room = Room();
        int count = held.LetAll(room);

        Assert.Equal(3, count);
        Assert.Equal(0, held.Count);
        Assert.Equal(new[] { 60, 64, 67 }, room[..count]);
    }

    /// <summary>A note from a pattern has no moment and is never let go of by the clock.</summary>
    [Fact]
    public void A_note_with_no_moment_never_expires()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60);

        Assert.Equal(0, held.LetExpired(long.MaxValue, Room()));
        Assert.Equal(1, held.Count);
    }

    /// <summary>A note played by hand is let go of once its moment has passed, and not before.</summary>
    [Fact]
    public void A_note_played_by_hand_expires_at_its_own_moment()
    {
        IHeldNotes held = new HeldNotes();

        held.Press(60, 1000);
        held.Press(64, 2000);

        var room = Room();

        Assert.Equal(0, held.LetExpired(999, room));

        Assert.Equal(1, held.LetExpired(1500, room));
        Assert.Equal(60, room[0]);
        Assert.True(held.Holds(64));

        Assert.Equal(1, held.LetExpired(2000, room));
        Assert.Equal(0, held.Count);
    }

    /// <summary>
    /// Past the limit the oldest note goes, and the caller is told which so it can end it.
    /// </summary>
    /// <remarks>
    /// A record that grew instead would fail further away and later, on the audio thread, after
    /// somebody had left a part sustaining for an hour.
    /// </remarks>
    [Fact]
    public void A_full_record_gives_up_its_oldest_note()
    {
        IHeldNotes held = new HeldNotes();

        for (int semitone = 0; semitone < HeldNotes.Most; semitone++) held.Press(semitone);

        int stolen = held.Press(100);

        Assert.Equal(0, stolen);
        Assert.Equal(HeldNotes.Most, held.Count);
        Assert.False(held.Holds(0));
        Assert.True(held.Holds(100));
    }

    /// <summary>Where there was room, nothing was taken to make it.</summary>
    [Fact]
    public void A_note_with_room_steals_nothing()
    {
        IHeldNotes held = new HeldNotes();

        Assert.Equal(-1, held.Press(60));
    }
}
