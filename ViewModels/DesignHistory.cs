using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using JingleBox2.Diagnostics;
using JingleBox2.Tracker.Machines;

namespace JingleBox2.ViewModels;

/// <summary>
/// What was done to a machine being designed, so it can be undone.
/// </summary>
/// <remarks>
/// The same principle as <see cref="Tracker.PatternHistory"/> and a different mechanism, because
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

    private const long MostBytes = 32L * 1024 * 1024;

    private static readonly JsonSerializerOptions Layout = new();

    private readonly List<string> _done = new();
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

    private long _bytes;

    /// <summary>True while a step is being put back, so putting one back is not itself a step.</summary>
    private bool _walking;

    public event Action? Changed;

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    /// <summary>True when what is on screen is not what is in the folder.</summary>
    public bool NeedsSaving => _now != _saved;

    /// <summary>Says the machine has just been written to disc, so this is what saved means now.</summary>
    public void Saved(MachineProject? project) 
    {
        _saved = Said(project);

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
    public bool Cancel(MachineProject? project)
    {
        if (project is null || !NeedsSaving || _saved.Length == 0) return false;

        _walking = true;

        try
        {
            if (!Take(project, _saved)) return false;

            _done.Clear();
            _undone.Clear();
            _now = _saved;

            Log.Write(LogArea.App, () => "design: threw away the changes to " + project.Name);

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
    public void Opened(MachineProject? project)
    {
        _done.Clear();
        _undone.Clear();
        _bytes = 0;
        _now = Said(project);

        // A machine just opened is a machine as its folder holds it, so there is nothing to save
        // and nothing to cancel.
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
    public void Did(MachineProject? project)
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
    public bool Undo(MachineProject? project) => Walk(_done, _undone, project, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public bool Redo(MachineProject? project) => Walk(_undone, _done, project, "did again");

    private bool Walk(List<string> from, List<string> onto, MachineProject? project, string said)
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

            Log.Write(LogArea.App, () => "design: " + said + " a change to " + project.Name);

            return true;
        }
        finally
        {
            _walking = false;

            Changed?.Invoke();
        }
    }

    private static string Said(MachineProject? project)
    {
        if (project is null) return "";

        try
        {
            return JsonSerializer.Serialize(project, Layout);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.App, () => "design: cannot keep a step: " + bad.Message);

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
    private static bool Take(MachineProject project, string said)
    {
        if (said.Length == 0) return false;

        try
        {
            var was = JsonSerializer.Deserialize<MachineProject>(said, Layout);
            if (was is null) return false;

            foreach (var property in typeof(MachineProject).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite) continue;
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

                property.SetValue(project, property.GetValue(was));
            }

            return true;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.App, () => "design: cannot put a step back: " + bad.Message);

            return false;
        }
    }
}
