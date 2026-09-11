using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One line of the take editor's history.
/// </summary>
/// <remarks>
/// The first line is the take as it was found rather than a step, which is what makes the list
/// something to stand on: going back to the top is going back to the file on the shelf.
///
/// **A row is kept and told what it says rather than made again**, which is not tidiness. The
/// list is what the picked line is read from, and a collection emptied and refilled loses which
/// line that was: from a chair the history would clear its own highlight every time anything was
/// done. Rows that outlive the change keep it.
/// </remarks>
public sealed partial class TakeStepRow : ObservableObject
{
    /// <summary>What the step is called.</summary>
    [ObservableProperty] private string words;

    /// <summary>
    /// True where this line has happened to the take you are looking at.
    /// </summary>
    /// <remarks>
    /// A line in front of where you are standing is still offered, since that is what redo would
    /// do again, and drawn fainter so the two kinds can be told apart.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fade))]
    private bool applied;

    /// <summary>How faint the line is drawn, which is fainter for a step in front of you.</summary>
    /// <remarks>
    /// On the row rather than through a converter, because there is one thing being decided and
    /// it is about this row: a converter here would be a file, a resource and a binding for one
    /// number that only ever has two values.
    /// </remarks>
    public double Fade => Applied ? 1 : 0.4;

    /// <summary>Names the line.</summary>
    /// <param name="words">What the step is called.</param>
    /// <param name="applied">True where it has happened to the take you are looking at.</param>
    public TakeStepRow(string words, bool applied)
    {
        this.words = words;
        this.applied = applied;
    }

    /// <summary>Tells a line it is about something else now, which is what a dropped step leaves.</summary>
    /// <param name="said">What it is called now.</param>
    /// <param name="done">True where it has happened to the take you are looking at.</param>
    public void Say(string said, bool done)
    {
        Words = said;
        Applied = done;
    }
}
