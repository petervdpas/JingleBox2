using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>
/// What was done to an instrument's settings, so it can be undone.
/// </summary>
/// <remarks>
/// The third history and the first with a real problem in it. The other two record things that
/// happen once: a note is typed, an element is dropped. A knob is not like that. Dragging one
/// across its range is one thing a person did and forty messages, and a controller sends a
/// hundred a second, so a step per message is a history nobody could ever walk. Whatever else
/// this does, it has to turn a stream back into a gesture.
///
/// The rule is the same control, within a moment. While a knob keeps moving, the step it began
/// stays where it is and no new one is made, so undo takes the whole sweep back to where it
/// started rather than a hundredth of it. Let go for longer than a moment, or touch a different
/// control, and the next move begins a new step.
///
/// Deliberately not "while the mouse is down", which would be true of the mouse and false of
/// everything else. A controller has no button to watch, automation has no hand at all, and the
/// rule has to be one thing for all three or the history means something different depending on
/// what you touched the knob with.
///
/// A step is the instrument as its own file holds it, minus the plugin's own patch. That last
/// part is not a shortcut: a described panel cannot change a plugin's state, so carrying three
/// hundred kilobytes of it in every step would be keeping the one thing that never moves.
/// </remarks>
public sealed class InstrumentHistory
{
    /// <summary>
    /// How long a control stays the same gesture after its last move.
    /// </summary>
    /// <remarks>
    /// Long enough to bridge a hand pausing mid-sweep and a controller's messages arriving in
    /// bursts, short enough that two deliberate nudges of the same knob are two steps. Half a
    /// second is about where a hand stops feeling like it is still doing the same thing.
    /// </remarks>
    public static readonly TimeSpan SameGesture = TimeSpan.FromMilliseconds(500);

    public const int MostSteps = 100;

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

    /// <summary>An instrument was opened. This is where it started.</summary>
    public void Opened(TrackerInstrument? instrument)
    {
        _done.Clear();
        _undone.Clear();
        _gathering = "";
        _now = Said(instrument);

        Changed?.Invoke();
    }

    /// <summary>
    /// A setting moved. Called after, with the key of whatever moved.
    /// </summary>
    public void Did(TrackerInstrument? instrument, string key)
    {
        if (_walking) return;

        string said = Said(instrument);
        if (said == _now) return;

        var at = _since.Elapsed;

        // Still the same control, and not long enough ago to be a second thought.
        bool same = key.Length > 0 && key == _gathering && at - _last < SameGesture;

        // Where it was before this message, which is what a step has to keep.
        string before = _now;

        _gathering = key;
        _last = at;
        _now = said;

        // The gesture that began this step is still going, so the step already holds where the
        // knob started and there is nothing to add. This is the whole of the gathering.
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

    /// <summary>Takes the last change back, into the instrument it was made on.</summary>
    public bool Undo(TrackerInstrument? instrument) => Walk(_done, _undone, instrument, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public bool Redo(TrackerInstrument? instrument) => Walk(_undone, _done, instrument, "did again");

    private bool Walk(List<string> from, List<string> onto, TrackerInstrument? instrument, string word)
    {
        if (instrument is null || from.Count == 0) return false;

        string wanted = from[^1];
        string here = _now;

        _walking = true;

        try
        {
            // Nothing is moved until the step is known to go back. A step that will not read is
            // dropped and everything under it is still good.
            if (!Take(instrument, wanted))
            {
                from.RemoveAt(from.Count - 1);

                Log.Write(LogArea.Tracker, () => "instrument: could not " + word + ", so that step is gone");

                return false;
            }

            from.RemoveAt(from.Count - 1);
            onto.Add(here);

            _now = wanted;

            // Whatever was being gathered is over: the thing under the hand is not where it was.
            _gathering = "";

            Log.Write(LogArea.Tracker, () => "instrument: " + word + " a change to " + instrument.Name);

            return true;
        }
        finally
        {
            _walking = false;

            Changed?.Invoke();
        }
    }

    /// <summary>Empties it, for a different instrument being opened.</summary>
    public void Forget() => Opened(null);

    /// <summary>True for the kind of thing something else is holding by reference.</summary>
    /// <remarks>
    /// A class the instrument owns rather than a value it holds. Strings and arrays are copied
    /// whole because nothing wraps one, and everything else that is a class is something a view
    /// model somewhere has a reference to.
    /// </remarks>
    private static bool Nested(Type type) =>
        type.IsClass && type != typeof(string) && !type.IsArray;

    /// <summary>Copies one object's settings into another, without either becoming the other.</summary>
    private static void Fill(object into, object from)
    {
        foreach (var property in into.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite) continue;

            property.SetValue(into, property.GetValue(from));
        }
    }

    private static string Said(TrackerInstrument? instrument)
    {
        if (instrument is null) return "";

        try
        {
            // The plugin's own patch is left out. A described panel cannot move it, so keeping
            // it would be carrying the one part of an instrument that never changes here.
            var was = instrument.PluginState;
            instrument.PluginState = Array.Empty<byte>();

            try
            {
                return JsonSerializer.Serialize(instrument, Layout);
            }
            finally
            {
                instrument.PluginState = was;
            }
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Tracker, () => "instrument: cannot keep a step: " + bad.Message);

            return "";
        }
    }

    /// <summary>
    /// Puts a step back into the instrument that is open, field by field.
    /// </summary>
    /// <remarks>
    /// In place, because the voice playing it, the panel drawing it and the song holding it all
    /// have this object. Every field but the plugin's patch, which was never in the step and
    /// must not be cleared by its absence.
    /// </remarks>
    private static bool Take(TrackerInstrument instrument, string said)
    {
        if (said.Length == 0) return false;

        try
        {
            var was = JsonSerializer.Deserialize<TrackerInstrument>(said, Layout);
            if (was is null) return false;

            foreach (var property in typeof(TrackerInstrument).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite) continue;
                if (property.Name == nameof(TrackerInstrument.PluginState)) continue;

                object? wanted = property.GetValue(was);
                object? here = property.GetValue(instrument);

                // The patch, the kit, the shape: things the panel's own view models wrap and
                // hold by reference. Handing over a new one leaves every knob on the panel
                // writing to an object the instrument no longer owns, so the sound stops
                // following the picture and the next edit records nothing at all. Poured into
                // rather than swapped, which is the same rule the song's patterns need.
                if (Nested(property.PropertyType) && wanted is not null && here is not null)
                {
                    Fill(here, wanted);

                    continue;
                }

                property.SetValue(instrument, wanted);
            }

            return true;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Tracker, () => "instrument: cannot put a step back: " + bad.Message);

            return false;
        }
    }
}
