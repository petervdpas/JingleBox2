using JingleBox2.Midi;
using JingleBox2.Tracker;
using System.Collections.Generic;
using Xunit;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// Which half of the application a key goes to, and whether its release follows it there.
/// </summary>
/// <remarks>
/// The rack takes notes while you are building a sound and the pattern takes them otherwise,
/// and the awkward cases are all the same shape: the answer to "which half" changing while a
/// key is down. Nothing stops somebody leaving the rack, opening a song or closing one with a
/// finger still on a key, and every one of those used to send the release to the half that
/// never heard the press. What is left behind is a note still sounding and a key still lit,
/// with nothing left to tell either of them the hand has gone.
/// </remarks>
public class NoteAdapterTests
{
    /// <summary>The plain case: the rack is in front, so both halves of the press go there.</summary>
    [Fact]
    public void A_key_goes_to_the_half_that_is_in_front()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "up 48" }, rack.Said);
        Assert.Empty(pattern.Said);
    }

    /// <summary>And the pattern takes them when it is the half in front.</summary>
    [Fact]
    public void And_to_the_other_one_when_that_is_in_front()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = false;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "up 48" }, pattern.Said);
        Assert.Empty(rack.Said);
    }

    /// <summary>Leaving the rack with a key held: the release still goes to the rack.</summary>
    [Fact]
    public void A_release_follows_its_press_when_the_page_changes_under_it()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        onTheRack = false;
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "up 48" }, rack.Said);
        Assert.Empty(pattern.Said);
    }

    /// <summary>And the same the other way about, which is opening a song with a key down.</summary>
    [Fact]
    public void In_either_direction()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = false;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        onTheRack = true;
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "up 48" }, pattern.Said);
        Assert.Empty(rack.Said);
    }

    /// <summary>A chord split by a page change: each key's release goes where its press went.</summary>
    [Fact]
    public void Every_key_held_is_remembered_on_its_own()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        adapter.TriggerNote(new Note(52), 100);

        onTheRack = false;
        adapter.TriggerNote(new Note(55), 100);

        adapter.ReleaseNote(new Note(52));
        adapter.ReleaseNote(new Note(55));
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "down 52", "up 52", "up 48" }, rack.Said);
        Assert.Equal(new[] { "down 55", "up 55" }, pattern.Said);
    }

    /// <summary>
    /// A release for a key nobody remembers goes to the half in front.
    /// </summary>
    /// <remarks>
    /// Which is what a device already holding a note when the program starts sends, and what
    /// arrives after a panic. Dropping it would be the one thing worse than sending it to the
    /// wrong half: nothing at all gets told the hand has gone.
    /// </remarks>
    [Fact]
    public void A_release_nobody_remembers_still_goes_somewhere()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = false;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "up 48" }, pattern.Said);
        Assert.Empty(rack.Said);
    }

    /// <summary>
    /// The same key pressed twice without a release in between is not two keys.
    /// </summary>
    /// <remarks>
    /// A keyboard that misses a note-off sends the next press of that key with nothing between
    /// them. One release is what arrives, and it has to be enough: a second remembered press
    /// would leave the first waiting for a release that is never coming. So both presses go to
    /// the rack, the release that was owed goes after them, and the one nobody owed goes to the
    /// half in front rather than nowhere.
    /// </remarks>
    [Fact]
    public void A_key_pressed_twice_takes_one_release()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);
        adapter.TriggerNote(new Note(48), 100);

        onTheRack = false;
        adapter.ReleaseNote(new Note(48));
        adapter.ReleaseNote(new Note(48));

        Assert.Equal(new[] { "down 48", "down 48", "up 48" }, rack.Said);
        Assert.Equal(new[] { "up 48" }, pattern.Said);
    }

    /// <summary>Two keys with the same name an octave apart are two different keys.</summary>
    [Fact]
    public void Keys_are_remembered_by_note_and_not_by_name()
    {
        var rack = new Half();
        var pattern = new Half();
        bool onTheRack = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => onTheRack);

        adapter.TriggerNote(new Note(48), 100);

        onTheRack = false;
        adapter.TriggerNote(new Note(60), 100);

        adapter.ReleaseNote(new Note(48));
        adapter.ReleaseNote(new Note(60));

        Assert.Equal(new[] { "down 48", "up 48" }, rack.Said);
        Assert.Equal(new[] { "down 60", "up 60" }, pattern.Said);
    }

    /// <summary>One of the two halves, and what it was told.</summary>
    private sealed class Half : IPlaysNotes
    {
        /// <summary>Each half of each press this side was handed, in order.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void PlayMidiNote(Note note, int volume) => Said.Add("down " + note.Semitone);

        /// <inheritdoc/>
        public void ReleaseMidiNote(Note note) => Said.Add("up " + note.Semitone);
    }
}
