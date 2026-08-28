using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using JingleBox2.Config;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.ViewModels;

/// <summary>
/// What was done to the pads, so it can be undone.
/// </summary>
/// <remarks>
/// The fourth history, and the smallest, because a pad is already a small piece of data: a name,
/// what it plays, a level, a colour. A step is every pad at once rather than the one that moved,
/// which costs almost nothing at this size and answers the question a per-pad history could not:
/// changing how many pads there are is an edit too, and it is not about any one of them.
///
/// Told after, like the designer's, and it works out for itself whether anything moved. Every
/// pad edit already ends at one place, which is where the settings get written, so there was
/// exactly one line to hook.
///
/// Gathered by which pad and which setting, the same rule the knobs use, because a pad's level
/// is a fader and a fader is a stream. Dragging one is one thing a person did.
/// </remarks>
public sealed class PadHistory
{
    /// <summary>
    /// How many steps are kept. A step is every pad written down as JSON, so a hundred of them
    /// is a few hundred kilobytes at the largest matrix there can be.
    /// </summary>
    public const int MostSteps = 100;

    /// <summary>How long the same setting on the same pad stays the same gesture.</summary>
    public static readonly TimeSpan SameGesture = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The plainest settings there are, on purpose: a step is never read by anything but this
    /// class, so nothing about it is worth spending a byte on.
    /// </summary>
    private static readonly JsonSerializerOptions Layout = new();

    /// <summary>What the pads were before each change, oldest first.</summary>
    private readonly List<string> _done = new();

    /// <summary>What was taken back, ready to be put on again. Emptied by any fresh edit.</summary>
    private readonly List<string> _undone = new();

    /// <summary>
    /// The clock the gathering window is measured against, rather than the wall clock: a
    /// gesture is a length of time and nobody cares what time of day it happened at.
    /// </summary>
    private readonly Stopwatch _since = Stopwatch.StartNew();

    /// <summary>The pads as they stand, which is what a step is measured against.</summary>
    private string _now = "";

    /// <summary>
    /// Which pad and which setting the last change was about, so the next one can be told
    /// whether it is more of the same gesture or the start of another.
    /// </summary>
    private string _gathering = "";

    /// <summary>When that last change arrived, on <see cref="_since"/>.</summary>
    private TimeSpan _last;

    /// <summary>Backing field for <see cref="Walking"/>.</summary>
    private bool _walking;

    /// <summary>
    /// Something moved, so whatever draws the undo and redo caps should read them again.
    /// </summary>
    /// <remarks>
    /// Raised for a gathered change as well, which changes neither cap: it costs nothing and
    /// leaving it out would mean the first nudge of a fader lit the cap and the rest did not.
    /// </remarks>
    public event Action? Changed;

    /// <summary>Whether there is anything to take back.</summary>
    public bool CanUndo => _done.Count > 0;

    /// <summary>Whether anything that was taken back can be put on again.</summary>
    public bool CanRedo => _undone.Count > 0;

    /// <summary>The pads as they stand. Nothing before this can be undone.</summary>
    public void Opened(IReadOnlyList<PadConfig>? pads)
    {
        _done.Clear();
        _undone.Clear();
        _gathering = "";
        _now = Said(pads);

        Changed?.Invoke();
    }

    /// <summary>
    /// Something about the pads moved. Called after, with what moved where.
    /// </summary>
    /// <param name="pads">
    /// Every pad as it stands after the change. A step is all of them at once, since how many
    /// there are is an edit too and it is about none of them in particular.
    /// </param>
    /// <param name="about">
    /// Which pad and which setting, as one word, so two nudges of the same fader gather and a
    /// nudge of a different one does not. Empty for a change that is about all of them, like how
    /// many there are, which is never gathered with anything.
    /// </param>
    public void Did(IReadOnlyList<PadConfig>? pads, string about)
    {
        if (_walking) return;

        string said = Said(pads);
        if (said == _now) return;

        var at = _since.Elapsed;

        bool same = about.Length > 0 && about == _gathering && at - _last < SameGesture;

        string before = _now;

        _gathering = about;
        _last = at;
        _now = said;

        if (same)
        {
            Changed?.Invoke();

            return;
        }

        _done.Add(before);

        if (_undone.Count > 0) _undone.Clear();

        while (_done.Count > MostSteps) _done.RemoveAt(0);

        Changed?.Invoke();
    }

    /// <summary>Takes the last change back. Answers the pads to put on, or nothing.</summary>
    public List<PadConfig>? Undo() => Walk(_done, _undone, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public List<PadConfig>? Redo() => Walk(_undone, _done, "did again");

    /// <summary>
    /// Moves one step from one pile to the other and answers the pads it holds.
    /// </summary>
    /// <remarks>
    /// A step that will not read back is dropped rather than left on top of the pile, where it
    /// would be pressed again and fail again with nothing said. Whatever it held is lost, which
    /// is the only honest answer once the text cannot be turned back into pads.
    ///
    /// The gathering is broken here on purpose: the change after an undo is a new gesture
    /// however quickly it follows, or it would be folded into the step that was just restored.
    /// </remarks>
    /// <param name="from">The pile the step is taken off, done for undo and undone for redo.</param>
    /// <param name="onto">The pile the pads as they stand now are put on, so the walk can be reversed.</param>
    /// <param name="word">What happened, for the log, in the words the log is written in.</param>
    /// <returns>The pads the step holds, or null when there was nothing to take or the step would not read.</returns>
    private List<PadConfig>? Walk(List<string> from, List<string> onto, string word)
    {
        if (from.Count == 0) return null;

        string wanted = from[^1];

        var pads = Read(wanted);

        if (pads is null)
        {
            from.RemoveAt(from.Count - 1);

            Log.Write(LogArea.App, () => "pads: could not " + word + ", so that step is gone");

            Changed?.Invoke();

            return null;
        }

        from.RemoveAt(from.Count - 1);
        onto.Add(_now);

        _now = wanted;
        _gathering = "";

        Log.Write(LogArea.App, () => "pads: " + word + " a change");

        Changed?.Invoke();

        return pads;
    }

    /// <summary>
    /// True while a step is going back, so putting one back is not itself a step.
    /// </summary>
    /// <remarks>
    /// Set by whoever applies the pads an undo answered, since applying them moves every pad
    /// and every one of those moves ends at the same line that records an edit. Without it, one
    /// undo would leave a step saying the pads had changed, and undo would never get anywhere.
    /// </remarks>
    public bool Walking
    {
        get => _walking;
        set => _walking = value;
    }

    /// <summary>
    /// The pads as one string, which is both the step and the way two states are compared.
    /// </summary>
    /// <remarks>
    /// A failure answers the empty string rather than throwing: a history that can stop an edit
    /// from happening is worse than one that quietly misses a step.
    /// </remarks>
    private static string Said(IReadOnlyList<PadConfig>? pads)
    {
        if (pads is null) return "";

        try
        {
            return JsonSerializer.Serialize(pads, Layout);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.App, () => "pads: cannot keep a step: " + bad.Message);

            return "";
        }
    }

    /// <summary>
    /// A step back into pads, or nothing when it cannot be read. See <see cref="Walk"/> for
    /// what happens to a step that answers nothing.
    /// </summary>
    private static List<PadConfig>? Read(string said)
    {
        if (said.Length == 0) return null;

        try
        {
            return JsonSerializer.Deserialize<List<PadConfig>>(said, Layout);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.App, () => "pads: cannot read a step back: " + bad.Message);

            return null;
        }
    }
}
