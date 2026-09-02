using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// What was done to a machine being designed, so it can be undone.
/// </summary>
/// <remarks>
/// The same principle as <see cref="Tracker.TrackerHistory"/> and a different mechanism, because
/// the document is a different shape. A pattern is one array of value types and a step is a
/// memory copy. A machine is a tree of elements, a list of parameters and a dozen fields beside
/// them, and copying that by hand means a clone method that will be right on the day it is
/// written and wrong the first time somebody adds a field.
///
/// So a step is the machine as its own file would hold it. That is not a trick: `machine.json`
/// is exactly the document being edited, the reader and writer already exist and are already
/// trusted with people's work, and a step written the same way cannot disagree with what a save
/// would produce. Fourteen kilobytes for a real machine, so a hundred steps is under two
/// megabytes.
///
/// Put back in place rather than as a new object. Panels, the rack and the utilities all hold
/// the project they were opened on, and handing the editor a different instance would leave
/// every one of them pointed at the machine as it was before the undo.
/// </remarks>
public sealed class DesignHistory
{
    /// <summary>How many steps are kept, and how much they may come to.</summary>
    public const int MostSteps = 100;

    /// <summary>
    /// The most the kept steps may come to before the oldest are let go.
    /// </summary>
    /// <remarks>
    /// A count on its own is not enough. A machine with a large picture on it is many times the
    /// size of a plain one, so a hundred steps is two megabytes for one machine and far more for
    /// another. The last step is always kept whatever it weighs, since a history of nothing is
    /// worse than a history that is over its allowance.
    /// </remarks>
    private const long MostBytes = 32L * 1024 * 1024;

    /// <summary>
    /// How a step is written down, which is the reader and writer's own defaults.
    /// </summary>
    /// <remarks>
    /// Deliberately plain: a step is never read by anybody, so indenting it would double what a
    /// hundred of them weigh and buy nothing.
    /// </remarks>
    private static readonly JsonSerializerOptions Layout = new();

    /// <summary>The states left behind, oldest first, each the machine as its file would hold it.</summary>
    private readonly List<string> _done = new();

    /// <summary>The states walked back out of, so redo has somewhere to go.</summary>
    /// <remarks>Emptied by the next real edit: there is one past and it has just been rewritten.</remarks>
    private readonly List<string> _undone = new();

    /// <summary>The machine as it stands, so a change has something to compare against.</summary>
    private string _now = "";

    /// <summary>And as it was when it was last written to disc.</summary>
    /// <remarks>
    /// A separate thing from the top of the undo stack. Undo walks the edits; this answers one
    /// question only, whether what is on screen is what is in the folder. Somebody can undo back
    /// to where they saved, and then the answer is no changes even though the history is full.
    /// </remarks>
    private string _saved = "";

    /// <summary>What the two lists come to, kept as they are added to rather than counted.</summary>
    private long _bytes;

    /// <summary>True while a step is being put back, so putting one back is not itself a step.</summary>
    private bool _walking;

    /// <summary>Raised whenever the answers below could have moved, so the buttons follow.</summary>
    public event Action? Changed;

    /// <summary>True when there is something to take back.</summary>
    public bool CanUndo => _done.Count > 0;

    /// <summary>True when something has been taken back and not yet put again.</summary>
    public bool CanRedo => _undone.Count > 0;

    /// <summary>True when what is on screen is not what is in the folder.</summary>
    public bool NeedsSaving => _now != _saved;

    /// <summary>
    /// Says the machine has just been written to disc, so this is what saved means now.
    /// </summary>
    /// <remarks>
    /// Both, and that is the whole of it: what is on disc is what is on screen at the moment it
    /// is written, by definition. Setting only the one on disc left the other holding whatever
    /// it last saw, and saving itself moves the machine: the version is bumped on the way out,
    /// so the file carries 1.12 while this went on believing the screen still said 1.11. The two
    /// then differ for ever, which is a Save button that goes green and never goes back and a
    /// Cancel changes that offers to throw away a change nobody made.
    ///
    /// It is not only the version. Anything a save does to the machine on its way past has the
    /// same effect, so the answer is not to hunt those down one at a time.
    /// </remarks>
    /// <param name="project">The machine as it was written.</param>
    public void Saved(IDesignProject? project)
    {
        _now = _saved = Said(project);

        Changed?.Invoke();
    }

    /// <summary>
    /// Throws away everything since the last save.
    /// </summary>
    /// <remarks>
    /// Not an undo of every step, which would walk back past the save and out the other side.
    /// It goes to one particular state, the one on disc, and empties the history because
    /// everything in it is now about a machine that no longer exists.
    /// </remarks>
    public bool Cancel(IDesignProject? project)
    {
        if (project is null || !NeedsSaving || _saved.Length == 0) return false;

        _walking = true;

        try
        {
            if (!Take(project, _saved)) return false;

            _done.Clear();
            _undone.Clear();
            _now = _saved;

            Log.Write(LogArea.Machines, () => "design: threw away the changes to " + project.Name);

            return true;
        }
        finally
        {
            _walking = false;

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// A machine was opened. This is where it started, and nothing before it can be undone.
    /// </summary>
    /// <remarks>
    /// What was just opened is the machine as its folder holds it, so saved starts equal to now:
    /// there is nothing to save and nothing to cancel until somebody does something.
    /// </remarks>
    public void Opened(IDesignProject? project)
    {
        _done.Clear();
        _undone.Clear();
        _bytes = 0;
        _now = Said(project);

        _saved = _now;

        Changed?.Invoke();
    }

    /// <summary>
    /// Something was done to it. Called after the edit, not before.
    /// </summary>
    /// <remarks>
    /// After, unlike the tracker's, because the designer says what it did rather than what it is
    /// about to do. It makes no difference: what gets kept is the state being left, and this
    /// holds on to that between calls.
    ///
    /// Called more often than there are edits, and deliberately so. Every redraw ends up here,
    /// including the ones where nothing about the machine moved, and those are recognised by the
    /// machine reading exactly as it did before and cost a comparison. Over-telling is safe;
    /// under-telling would be an edit that cannot be undone.
    /// </remarks>
    public void Did(IDesignProject? project)
    {
        if (_walking) return;

        string said = Said(project);

        if (said == _now) return;

        _done.Add(_now);
        _bytes += _now.Length;

        _now = said;

        if (_undone.Count > 0) _undone.Clear();

        while (_done.Count > MostSteps || (_bytes > MostBytes && _done.Count > 1))
        {
            _bytes -= _done[0].Length;
            _done.RemoveAt(0);
        }

        Changed?.Invoke();
    }

    /// <summary>Takes the last change back, into the project it was made on.</summary>
    public bool Undo(IDesignProject? project) => Walk(_done, _undone, project, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public bool Redo(IDesignProject? project) => Walk(_undone, _done, project, "did again");

    /// <summary>
    /// Moves one step from one list to the other and puts the machine where that step says.
    /// </summary>
    /// <remarks>
    /// Undo and redo are the same walk in opposite directions, written once so the two cannot
    /// drift apart. The walking flag is up throughout, since putting a step back sets every field
    /// on the project and the editor reports each of those as an edit.
    /// </remarks>
    /// <param name="from">The list the step is taken off, done for undo and undone for redo.</param>
    /// <param name="onto">The list where the machine as it stands now is put, so the walk can be reversed.</param>
    /// <param name="project">The machine being designed, poured into in place rather than replaced.</param>
    /// <param name="said">The word for the log, which is the only difference between the two.</param>
    /// <returns>True when a step was taken, false when there was nothing to take or no project to put it on.</returns>
    private bool Walk(List<string> from, List<string> onto, IDesignProject? project, string said)
    {
        if (project is null || from.Count == 0) return false;

        string wanted = from[^1];
        from.RemoveAt(from.Count - 1);
        _bytes -= wanted.Length;

        onto.Add(_now);
        _bytes += _now.Length;

        _walking = true;

        try
        {
            if (!Take(project, wanted)) return false;

            _now = wanted;

            Log.Write(LogArea.Machines, () => "design: " + said + " a change to " + project.Name);

            return true;
        }
        finally
        {
            _walking = false;

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The machine written down, which is what a step is.
    /// </summary>
    /// <remarks>
    /// An empty string for a machine that will not serialise, and for no machine at all. Both are
    /// then refused by <see cref="Take"/> rather than being put back as an empty machine, which is
    /// the difference between an undo that does nothing and an undo that loses the work.
    /// </remarks>
    private static string Said(IDesignProject? project)
    {
        if (project is null) return "";

        try
        {
            return JsonSerializer.Serialize(project, project.GetType(), Layout);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Machines, () => "design: cannot keep a step: " + bad.Message);

            return "";
        }
    }

    /// <summary>
    /// Puts a kept step back onto the project that is open, field by field.
    /// </summary>
    /// <remarks>
    /// Every property the file holds, found rather than listed. A list written out here would be
    /// right the day it was written and wrong the first time a field is added to a machine, and
    /// the way that fails is the worst kind: an undo that silently drops whatever was forgotten.
    /// What the file carries is exactly what has to come back, and the file already says which
    /// those are.
    ///
    /// The ones marked as not belonging in the file are left alone, which is what keeps the
    /// folder a machine came from pointing where it did.
    /// </remarks>
    private static bool Take(IDesignProject project, string said)
    {
        if (said.Length == 0) return false;

        try
        {
            var was = JsonSerializer.Deserialize(said, project.GetType(), Layout);
            if (was is null) return false;

            foreach (var property in project.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite) continue;
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

                property.SetValue(project, property.GetValue(was));
            }

            return true;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Machines, () => "design: cannot put a step back: " + bad.Message);

            return false;
        }
    }
}
