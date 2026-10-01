using System.Collections.ObjectModel;
using JingleBox2.Audio.Records;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>Typing a command into a cell: its letter on one stop and its amount on the next.</summary>
/// <remarks>
/// The letter and the amount are two stops because A to F are both letters and digits, and with
/// one stop for both the commands named by those letters could not be typed at all.
/// </remarks>
public class CommandTypingTests
{
    /// <summary>The editing rules on their own.</summary>
    private readonly PatternEdit _edits = new();

    /// <summary>A small pattern to type into.</summary>
    private static Pattern Empty() => new(16, 4);

    /// <summary>The cursor on line nought of track nought, on the stop given.</summary>
    private static PatternCursor At(CellColumn column) => new(0, 0, column);

    /// <summary>Every letter, the six that are also digits included, names a command.</summary>
    [Theory]
    [InlineData('A')]
    [InlineData('C')]
    [InlineData('F')]
    [InlineData('Q')]
    [InlineData('r')]
    [InlineData('7')]
    public void Any_letter_or_digit_names_a_command(char typed)
    {
        var pattern = Empty();

        Assert.True(_edits.EnterEffectCommand(pattern, At(CellColumn.Effect), typed));
        Assert.Equal(char.ToUpperInvariant(typed), pattern[0, 0].Effect.Command);
    }

    /// <summary>Anything that is not a letter or a digit names nothing.</summary>
    [Theory]
    [InlineData(' ')]
    [InlineData('-')]
    [InlineData('é')]
    public void Anything_else_names_nothing(char typed)
    {
        var pattern = Empty();

        Assert.False(_edits.EnterEffectCommand(pattern, At(CellColumn.Effect), typed));
        Assert.True(pattern[0, 0].Effect.IsNone);
    }

    /// <summary>A letter typed on the amount stop names nothing.</summary>
    [Fact]
    public void A_letter_on_the_amount_names_nothing()
    {
        var pattern = Empty();

        Assert.False(_edits.EnterEffectCommand(pattern, At(CellColumn.Amount), 'Q'));
    }

    /// <summary>The amount takes two hex digits after the letter, keeping the letter.</summary>
    [Fact]
    public void The_amount_takes_two_digits()
    {
        var pattern = Empty();

        _edits.EnterEffectCommand(pattern, At(CellColumn.Effect), 'A');
        _edits.EnterHexDigit(pattern, At(CellColumn.Amount), '4');
        _edits.EnterHexDigit(pattern, At(CellColumn.Amount), '7');

        Assert.Equal(new TrackerCommand('A', 0x47), pattern[0, 0].Effect);
    }

    /// <summary>A digit on the amount of a cell with no command does nothing, rather than inventing one.</summary>
    [Fact]
    public void An_amount_with_no_command_is_refused()
    {
        var pattern = Empty();

        Assert.False(_edits.EnterHexDigit(pattern, At(CellColumn.Amount), '4'));
        Assert.True(pattern[0, 0].Effect.IsNone);
    }

    /// <summary>A new letter keeps the amount that was there.</summary>
    [Fact]
    public void A_new_letter_keeps_the_amount()
    {
        var pattern = Empty();
        pattern[0, 0] = pattern[0, 0] with { Effect = new TrackerCommand('Q', 0x06) };

        _edits.EnterEffectCommand(pattern, At(CellColumn.Effect), 'C');

        Assert.Equal(new TrackerCommand('C', 0x06), pattern[0, 0].Effect);
    }

    /// <summary>Delete on either stop clears the whole command.</summary>
    [Theory]
    [InlineData(CellColumn.Effect)]
    [InlineData(CellColumn.Amount)]
    public void Delete_on_either_stop_clears_the_command(CellColumn column)
    {
        var pattern = Empty();
        pattern[0, 0] = new TrackerCell(new Note(48), 0, 0x40, new TrackerCommand('R', 0x04));

        _edits.ClearAtCursor(pattern, At(column));

        Assert.True(pattern[0, 0].Effect.IsNone);
        Assert.Equal(48, pattern[0, 0].Note.Semitone);
    }

    /// <summary>The letter and its amount sit together, with no gap, and a click on each lands on it.</summary>
    [Fact]
    public void The_letter_and_amount_sit_together()
    {
        var metrics = new PatternMetrics(10, 18, 4);

        double letter = metrics.ColumnX(0, CellColumn.Effect);
        double amount = metrics.ColumnX(0, CellColumn.Amount);

        Assert.Equal(letter + 10, amount);
        Assert.Equal(CellColumn.Effect, metrics.ColumnAt(letter + 5, 0));
        Assert.Equal(CellColumn.Amount, metrics.ColumnAt(amount + 15, 0));
    }

    /// <summary>Typed into the tracker: the letter moves the cursor on to the amount, and two digits step down once.</summary>
    [Fact]
    public void Typing_a_command_moves_on_and_steps_down_once()
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());

        tracker.IsRecording = true;
        tracker.EditStep = 1;
        tracker.Cursor = new PatternCursor(0, 0, CellColumn.Effect);

        tracker.EnterEffectCommand('Q');
        Assert.Equal(CellColumn.Amount, tracker.Cursor.Column);

        tracker.EnterHexDigit('0');
        Assert.Equal(0, tracker.Cursor.Line);

        tracker.EnterHexDigit('6');
        Assert.Equal(1, tracker.Cursor.Line);

        Assert.Equal(new TrackerCommand('Q', 0x06), tracker.Song.Patterns[0][0, 0].Effect);

        tracker.Finished();
    }
}
