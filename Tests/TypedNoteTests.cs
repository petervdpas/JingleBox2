using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a key with no velocity sensor sends, and how far the octave keys go.
/// </summary>
/// <remarks>
/// The letter rows stand in for a keyboard, and the two things a keyboard does that they had no
/// answer for are how hard a key was hit and which octave it is in. Both are decisions rather
/// than absences, and both are taken here so they can be put a question to without a window.
/// </remarks>
public class TypedNoteTests
{
    /// <summary>A key at full strength is 0x7F and never the level above it.</summary>
    /// <remarks>
    /// The column runs to 0x80 and that top step is the one level no key can produce, typed
    /// rather than played. A letter row is producing a key press, so it writes what the hardest
    /// possible key press writes. It is also Renoise's 127, said in hexadecimal.
    /// </remarks>
    [Fact]
    public void A_typed_key_sends_what_the_hardest_played_key_sends()
    {
        Assert.Equal(0x7F, TrackerViewModel.TypedLevel);
        Assert.True(TrackerViewModel.TypedLevel < TrackerCell.MaxVolume);
    }

    /// <summary>The octave holds at both ends rather than coming round.</summary>
    /// <remarks>
    /// An octave that wrapped from the top to nought would put a part eight octaves out for one
    /// keystroke too many, and the note is written the instant it is typed.
    /// </remarks>
    [Fact]
    public void The_octave_holds_at_its_ends()
    {
        Assert.Equal(0, TrackerViewModel.LeastOctave);
        Assert.Equal(9, TrackerViewModel.MostOctave);
    }
}
