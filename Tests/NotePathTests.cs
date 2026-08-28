using JingleBox2.Midi;
using JingleBox2.Tracker;
using System.Collections.Generic;
using Xunit;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// A key on a MIDI keyboard, from the bytes to the two halves of the press.
/// </summary>
/// <remarks>
/// The wire is tested next door and the keyboard's light is tested beside that; this is the
/// piece between them, which is where a key that lights and never goes dark would come from.
/// Both halves of a press have to arrive, and they have to name the same note, or whoever is
/// holding the light has nothing to put it out with.
///
/// <see cref="MidiService.Read"/> and <see cref="INoteTrigger"/> are both public, so the whole
/// of it can be played without a keyboard plugged in.
/// </remarks>
public class NotePathTests
{
    /// <summary>The plain case: a press and a release, each naming the same note.</summary>
    [Fact]
    public void A_key_pressed_and_let_go_of_arrives_as_both_halves()
    {
        var heard = Play(new byte[] { 0x90, 60, 100 }, new byte[] { 0x80, 60, 0 });

        Assert.Equal(new[] { "down 48", "up 48" }, heard);
    }

    /// <summary>
    /// The other spelling of a release, which is what most keyboards actually send.
    /// </summary>
    /// <remarks>
    /// A note on at no velocity. Read as a press it would be a key that goes down and never
    /// comes up, which is exactly what a stuck light looks like.
    /// </remarks>
    [Fact]
    public void A_note_on_at_no_velocity_is_the_release()
    {
        var heard = Play(new byte[] { 0x90, 60, 100 }, new byte[] { 0x90, 60, 0 });

        Assert.Equal(new[] { "down 48", "up 48" }, heard);
    }

    /// <summary>
    /// And through running status, where the release carries no status byte of its own.
    /// </summary>
    /// <remarks>
    /// A keyboard playing a run sends the status once and the pairs after it bare. The release
    /// of the last note is then three bytes that look like nothing at all unless the reader
    /// remembers what the device was last saying.
    /// </remarks>
    [Fact]
    public void And_through_running_status()
    {
        var heard = Play(
            new byte[] { 0x90, 60, 100 },
            new byte[] { 64, 100 },
            new byte[] { 60, 0 },
            new byte[] { 64, 0 });

        Assert.Equal(new[] { "down 48", "down 52", "up 48", "up 52" }, heard);
    }

    /// <summary>A chord: three down, three up, each naming its own note.</summary>
    [Fact]
    public void A_chord_arrives_key_by_key()
    {
        var heard = Play(
            new byte[] { 0x90, 60, 100 },
            new byte[] { 0x90, 64, 100 },
            new byte[] { 0x90, 67, 100 },
            new byte[] { 0x80, 64, 0 },
            new byte[] { 0x80, 60, 0 },
            new byte[] { 0x80, 67, 0 });

        Assert.Equal(
            new[] { "down 48", "down 52", "down 55", "up 52", "up 48", "up 55" },
            heard);
    }

    /// <summary>
    /// Pressure is not a key coming up.
    /// </summary>
    /// <remarks>
    /// An aftertouch message shares the shape of a note and means something else entirely. Read
    /// as one it would put a key out while a finger was still leaning on it.
    /// </remarks>
    [Fact]
    public void Pressure_on_a_held_key_is_not_a_release()
    {
        var heard = Play(
            new byte[] { 0x90, 60, 100 },
            new byte[] { 0xA0, 60, 90 },
            new byte[] { 0xA0, 60, 20 });

        Assert.Equal(new[] { "down 48" }, heard);
    }

    /// <summary>Plays those messages through the wire and the router, and says what was heard.</summary>
    private static IReadOnlyList<string> Play(params byte[][] messages)
    {
        var service = new MidiService();
        var heard = new Keys();
        var router = new MidiNoteRouter(heard);

        foreach (var bytes in messages)
        {
            var message = service.Read("keyboard", bytes, 0, bytes.Length);
            if (message != null) router.Handle(message);
        }

        return heard.Said;
    }

    /// <summary>Somewhere for the two halves to land, in the order they landed.</summary>
    private sealed class Keys : INoteTrigger
    {
        /// <summary>Each half of each press, in the order it arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void TriggerNote(Note note, int volume) => Said.Add("down " + note.Semitone);

        /// <inheritdoc/>
        public void ReleaseNote(Note note) => Said.Add("up " + note.Semitone);
    }
}
