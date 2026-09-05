using System;
using System.Linq;
using Avalonia.Input;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The one table of what the pattern answers, and the two things it has to get right.
/// </summary>
/// <remarks>
/// **A key was written down in three places**: the view's own handling decided what it did, a
/// list beside it filled the card in SETTINGS, and the help said it again in prose. So a key
/// added appeared nowhere and a key changed disagreed with the other two. There is one table now
/// and everything reads it, which is the rule this codebase already wrote down once and then
/// broke.
/// </remarks>
public class PatternKeyTests
{
    private readonly PatternKeys _keys = new();

    /// <summary>
    /// The most particular key wins, whatever order the rows happen to be in.
    /// </summary>
    /// <remarks>
    /// This is a trap that has already been fallen into here, in a switch statement: `Ctrl+V` is
    /// matched by asking whether control is held, which holding shift as well does not stop being
    /// true, so `Ctrl+Shift+V` written underneath it never fired and pasted instead. A table
    /// cannot be got wrong that way, and this says so rather than trusting it.
    /// </remarks>
    [Fact]
    public void The_narrower_key_wins_over_the_wider_one()
    {
        Assert.Equal(PatternAction.Paste, _keys.Find(Key.V, KeyModifiers.Control));
        Assert.Equal(PatternAction.TypedVelocity,
            _keys.Find(Key.V, KeyModifiers.Control | KeyModifiers.Shift));
    }

    /// <summary>Shift on a cursor key says how far a block reaches, not which key it is.</summary>
    [Fact]
    public void Shift_does_not_change_which_key_was_pressed()
    {
        Assert.Equal(PatternAction.CursorUp, _keys.Find(Key.Up, KeyModifiers.None));
        Assert.Equal(PatternAction.CursorUp, _keys.Find(Key.Up, KeyModifiers.Shift));
        Assert.Equal(PatternAction.NextTrack, _keys.Find(Key.Tab, KeyModifiers.Shift));
    }

    /// <summary>A key the pattern does not answer is left alone.</summary>
    /// <remarks>
    /// It has to be, or the letter rows would stop being a keyboard: every note is typed on a key
    /// this table has never heard of, and one claimed here is a note nobody can enter.
    /// </remarks>
    [Fact]
    public void A_key_that_is_not_the_pattern_s_is_not_taken()
    {
        Assert.Equal(PatternAction.None, _keys.Find(Key.Z, KeyModifiers.None));
        Assert.Equal(PatternAction.None, _keys.Find(Key.Q, KeyModifiers.None));
        Assert.Equal(PatternAction.None, _keys.Find(Key.D1, KeyModifiers.None));
        Assert.Equal(PatternAction.None, _keys.Find(Key.S, KeyModifiers.Control));
    }

    /// <summary>
    /// Every action has a key and words, and every key names an action.
    /// </summary>
    /// <remarks>
    /// The whole of what the table is for. An action with no key cannot be asked for, and one
    /// with no words is a key the card in SETTINGS and the help page cannot name, which is how a
    /// key comes to be answered and undiscoverable at the same time.
    /// </remarks>
    [Fact]
    public void Every_action_has_a_key_and_words()
    {
        foreach (PatternAction action in Enum.GetValues<PatternAction>())
        {
            if (action == PatternAction.None) continue;

            Assert.True(_keys.All.Any(row => row.Does == action), action + " is on no key");
            Assert.False(string.IsNullOrWhiteSpace(PatternKeys.Words(action)), action + " has no words");
        }

        foreach (var row in _keys.All)
        {
            Assert.NotEqual(PatternAction.None, row.Does);
            Assert.False(string.IsNullOrWhiteSpace(row.Said), row.Key + " has no name to show");
        }
    }

    /// <summary>Every key in the table is found again by pressing it.</summary>
    /// <remarks>
    /// A row that cannot be reached is a line on a help page for a key that does nothing, which
    /// is worse than the key being missing: somebody presses it and doubts the rest of the page.
    /// </remarks>
    [Fact]
    public void Every_key_in_the_table_answers()
    {
        foreach (var row in _keys.All)
            Assert.Equal(row.Does, _keys.Find(row.Key, row.Modifiers));
    }

    /// <summary>The list a page shows is one line per action, naming every key it is on.</summary>
    /// <remarks>
    /// Per action rather than per key, which is what lets the octave read as one line naming both
    /// the keypad and the brackets rather than as two lines saying nearly the same thing.
    /// </remarks>
    [Fact]
    public void The_list_is_one_line_an_action()
    {
        var listed = _keys.Listed;

        Assert.Equal(_keys.All.Select(row => row.Does).Distinct().Count(), listed.Count);

        var octave = listed.Single(row => row.Does.Contains("up a step"));

        Assert.Contains("Numpad *", octave.Keys);
        Assert.Contains("Ctrl+]", octave.Keys);
    }
}
