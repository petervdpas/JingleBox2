using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// Where the three octaves on a panel have to be for a note to be on them.
/// </summary>
/// <remarks>
/// The rule a keyboard follows when a note arrives from somewhere it is not looking. It moves
/// as little as it can, because a keyboard that recentred on every note would be a keyboard
/// nobody could read a run on.
/// </remarks>
public class PanelKeyboardTests
{
    /// <summary>Thirty-seven keys starting at octave 4 is C-4 to C-7.</summary>
    /// <remarks>
    /// Both ends of that stretch are on show, C-7 being the last key, which is the C on top of
    /// the three octaves, so neither of them is a reason to move.
    /// </remarks>
    [Fact]
    public void A_note_already_on_show_moves_nothing()
    {
        Assert.Equal(4, PanelKeyboard.Reveal(new Note(4 * 12), 4));
        Assert.Equal(4, PanelKeyboard.Reveal(new Note(5 * 12 + 7), 4));
        Assert.Equal(4, PanelKeyboard.Reveal(new Note(7 * 12), 4));
    }

    /// <summary>Below the keyboard, the note's own octave becomes the leftmost one.</summary>
    [Fact]
    public void A_note_below_puts_its_own_octave_on_the_left()
    {
        Assert.Equal(2, PanelKeyboard.Reveal(new Note(2 * 12 + 3), 4));
    }

    /// <summary>Above, it travels the least distance that puts the note on the keyboard.</summary>
    [Fact]
    public void A_note_above_becomes_the_rightmost_octave()
    {
        Assert.Equal(5, PanelKeyboard.Reveal(new Note(7 * 12 + 1), 4));
        Assert.Equal(6, PanelKeyboard.Reveal(new Note(8 * 12 + 5), 4));
    }

    /// <summary>
    /// The ends of the range, which are B-9 at the top and C-0 at the bottom.
    /// </summary>
    /// <remarks>
    /// The highest note does not put the keyboard at the highest octave: three octaves starting
    /// at 7 already reach it, and starting at 9 would be a keyboard mostly showing keys that do
    /// not exist.
    ///
    /// A note past either end is not a note at all, and moves nothing.
    /// </remarks>
    [Fact]
    public void And_never_past_the_ends_of_the_range()
    {
        Assert.Equal(7, PanelKeyboard.Reveal(new Note(Note.MaxSemitone), 4));
        Assert.Equal(0, PanelKeyboard.Reveal(new Note(Note.MinSemitone), 4));
        Assert.Equal(4, PanelKeyboard.Reveal(new Note(127), 4));
    }

    /// <summary>A blank or note-off cell is not a pitch, and moves nothing.</summary>
    [Fact]
    public void A_note_that_is_not_a_note_moves_nothing()
    {
        Assert.Equal(4, PanelKeyboard.Reveal(Note.Empty, 4));
        Assert.Equal(4, PanelKeyboard.Reveal(Note.Off, 4));
    }
}
