using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using JingleBox2.Config;
using JingleBox2.Diagnostics;

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
    public const int MostSteps = 100;

    /// <summary>How long the same setting on the same pad stays the same gesture.</summary>
    public static readonly TimeSpan SameGesture = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions Layout = new();

    private readonly List<string> _done = new();
    private readonly List<string> _undone = new();

    private readonly Stopwatch _since = Stopwatch.StartNew();

    private string _now = "";
    private string _gathering = "";
    private TimeSpan _last;

    private bool _walking;

    public event Action? Changed;

    public bool CanUndo => _done.Count > 0;
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

    private List<PadConfig>? Walk(List<string> from, List<string> onto, string word)
    {
        if (from.Count == 0) return null;

        string wanted = from[^1];

        var pads = Read(wanted);

        // A step that will not read is dropped rather than left at the top to be pressed again.
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

    /// <summary>True while a step is going back, so putting one back is not itself a step.</summary>
    public bool Walking
    {
        get => _walking;
        set => _walking = value;
    }

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
