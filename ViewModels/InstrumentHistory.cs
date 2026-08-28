using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Tracker;
using JingleBox2.Diagnostics.Enums;

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

    /// <summary>How many steps are kept.</summary>
    /// <remarks>
    /// A count and no weight, unlike the machine designer's, because an instrument without its
    /// plugin patch is a few kilobytes whatever it is: there is no picture in it to make one step
    /// far heavier than another.
    /// </remarks>
    public const int MostSteps = 100;

    /// <summary>How a step is written down, which is the reader and writer's own defaults.</summary>
    private static readonly JsonSerializerOptions Layout = new();

    /// <summary>The states left behind, oldest first, each the instrument as its file holds it.</summary>
    private readonly List<string> _done = new();

    /// <summary>The states walked back out of, so redo has somewhere to go.</summary>
    private readonly List<string> _undone = new();

    /// <summary>
    /// The clock the gathering is measured against.
    /// </summary>
    /// <remarks>
    /// A stopwatch rather than the wall clock, since the only question asked of it is how long ago
    /// something was and the wall clock can be put back underneath the answer.
    /// </remarks>
    private readonly Stopwatch _since = Stopwatch.StartNew();

    /// <summary>The instrument as it stands, so a change has something to compare against.</summary>
    private string _now = "";

    /// <summary>Which control the step being gathered belongs to, or empty when none is.</summary>
    private string _gathering = "";

    /// <summary>When that control last moved, which is what ends the gesture.</summary>
    private TimeSpan _last;

    /// <summary>True while a step is being put back, so putting one back is not itself a step.</summary>
    private bool _walking;

    /// <summary>Raised whenever the answers below could have moved, so the buttons follow.</summary>
    public event Action? Changed;

    /// <summary>True when there is something to take back.</summary>
    public bool CanUndo => _done.Count > 0;

    /// <summary>True when something has been taken back and not yet put again.</summary>
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
    /// <remarks>
    /// A move that leaves the instrument reading exactly as it did leaves no step, which is what
    /// makes it safe to say this more often than there are edits.
    ///
    /// While the same control keeps moving inside <see cref="SameGesture"/>, the step already
    /// holds where the knob started and nothing is added: that is the whole of the gathering.
    /// A different key, or a longer gap, begins a new step and what is kept is where the
    /// instrument stood before this message.
    /// </remarks>
    /// <param name="instrument">
    /// The instrument as it stands after the move, read for the step that keeps where it stood
    /// before it.
    /// </param>
    /// <param name="key">
    /// What moved, as a panel names it. An empty key never gathers, so a change nobody can name
    /// is its own step rather than being folded into whatever was last touched.
    /// </param>
    public void Did(TrackerInstrument? instrument, string key)
    {
        if (_walking) return;

        string said = Said(instrument);
        if (said == _now) return;

        var at = _since.Elapsed;

        bool same = key.Length > 0 && key == _gathering && at - _last < SameGesture;

        string before = _now;

        _gathering = key;
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

    /// <summary>Takes the last change back, into the instrument it was made on.</summary>
    public bool Undo(TrackerInstrument? instrument) => Walk(_done, _undone, instrument, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public bool Redo(TrackerInstrument? instrument) => Walk(_undone, _done, instrument, "did again");

    /// <summary>
    /// Moves one step from one list to the other and puts the instrument where that step says.
    /// </summary>
    /// <remarks>
    /// Nothing is moved until the step is known to go back. A step that will not read is dropped
    /// and everything under it is still good, which is why the list is only shortened after
    /// <see cref="Take"/> has been tried rather than before.
    ///
    /// Whatever was being gathered is over once this has run: the thing under the hand is not
    /// where it was, so the next move begins a step of its own rather than joining a gesture that
    /// has just been undone.
    /// </remarks>
    /// <param name="from">The list the step is taken off, done for undo and undone for redo.</param>
    /// <param name="onto">The list where the instrument as it reads now is put, so the walk can be reversed.</param>
    /// <param name="instrument">The instrument to pour the step into, in place, since the panel holds it by reference.</param>
    /// <param name="word">The word for the log, which is the only difference between the two.</param>
    /// <returns>True when a step was put back, false when there was nothing to take or the step would not read.</returns>
    private bool Walk(List<string> from, List<string> onto, TrackerInstrument? instrument, string word)
    {
        if (instrument is null || from.Count == 0) return false;

        string wanted = from[^1];
        string here = _now;

        _walking = true;

        try
        {
            if (!Take(instrument, wanted))
            {
                from.RemoveAt(from.Count - 1);

                Log.Write(LogArea.Tracker, () => "instrument: could not " + word + ", so that step is gone");

                return false;
            }

            from.RemoveAt(from.Count - 1);
            onto.Add(here);

            _now = wanted;

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

    /// <summary>
    /// The instrument written down, which is what a step is.
    /// </summary>
    /// <remarks>
    /// The plugin's own patch is left out and put straight back. A described panel cannot move it,
    /// so keeping it would be carrying the one part of an instrument that never changes here, and
    /// it is a third of a megabyte apiece.
    ///
    /// An empty string for an instrument that will not serialise, and for no instrument at all;
    /// <see cref="Take"/> refuses both rather than putting back an empty instrument.
    /// </remarks>
    private static string Said(TrackerInstrument? instrument)
    {
        if (instrument is null) return "";

        try
        {
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
    ///
    /// The patch, the kit and the shape are poured into rather than swapped. Those are things the
    /// panel's own view models wrap and hold by reference: handing over a new one leaves every
    /// knob on the panel writing to an object the instrument no longer owns, so the sound stops
    /// following the picture and the next edit records nothing at all. Same rule the song's
    /// patterns need, for the same reason.
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
