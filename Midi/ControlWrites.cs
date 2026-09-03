using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// One trip to the drawing thread is booked for however much arrives before it runs, which is
/// what makes a sweep across a knob cost one write rather than a hundred and twenty eight. The
/// trip is handed in rather than taken here, so this can be pumped by hand in a test and by the
/// dispatcher in the application.
///
/// Presses are held to <see cref="Most"/> in one trip, and past that the newest are dropped. A
/// limit that grows is a limit that fails further away, which is the rule this codebase already
/// keeps about the notes a plugin is holding: a hand cannot make sixty four presses between two
/// frames, so anything past that is a device sending nonsense or a drawing thread that has
/// stopped, and neither is helped by a list that grows until the memory does.
/// </remarks>
public sealed class ControlWrites : IControlWrites
{
    /// <summary>How many presses one trip to the drawing thread will carry.</summary>
    private const int Most = 64;

    /// <summary>How a piece of work is put on the drawing thread.</summary>
    private readonly Action<Action> _post;

    /// <summary>What is waiting for a parameter, one value per link.</summary>
    private readonly Dictionary<ControlMapping, (Action<double> Write, double Value)> _waiting = new();

    /// <summary>The presses waiting, in the order they were made.</summary>
    private readonly List<(Action<double> Write, double Value)> _pressed = new();

    /// <summary>Whether a trip to the drawing thread is already booked.</summary>
    private bool _posted;

    /// <summary>Makes one, over whatever puts work on the drawing thread.</summary>
    /// <param name="post">
    /// How to get onto the drawing thread. Handed in because the application has a dispatcher
    /// and a test has none, and because the whole of what is worth checking here is what happens
    /// between the booking and the trip.
    /// </param>
    public ControlWrites(Action<Action> post) => _post = post;

    /// <inheritdoc/>
    public void Moved(ControlMapping mapping, Action<double> write, double value)
    {
        lock (_waiting)
        {
            _waiting[mapping] = (write, value);

            if (Booked()) return;
        }

        _post(Run);
    }

    /// <inheritdoc/>
    public void Pressed(Action<double> write, double value)
    {
        lock (_waiting)
        {
            if (_pressed.Count >= Most)
            {
                Log.Write(LogArea.Midi, () =>
                    "controls: more than " + Most + " presses arrived before the screen could be "
                    + "drawn, so the rest are dropped");

                return;
            }

            _pressed.Add((write, value));

            if (Booked()) return;
        }

        _post(Run);
    }

    /// <inheritdoc/>
    public double? Waiting(ControlMapping mapping)
    {
        lock (_waiting)
            return _waiting.TryGetValue(mapping, out var held) ? held.Value : null;
    }

    /// <summary>Whether the trip was already asked for, and books it where it was not.</summary>
    /// <remarks>Called under the lock, since the flag and the two lists are one fact.</remarks>
    private bool Booked()
    {
        if (_posted) return true;

        _posted = true;

        return false;
    }

    /// <summary>
    /// Puts everything that is waiting where it goes, on the drawing thread.
    /// </summary>
    /// <remarks>
    /// The presses first and in order, then the values. A press is a thing that happened and a
    /// value is where something ended up, so a pad hit and then turned down should sound and then
    /// be quieter rather than the other way about.
    ///
    /// Each write is swallowed on its own: one parameter that will not take a value is one knob
    /// gone quiet, and letting it throw here would take the rest of the desk with it.
    /// </remarks>
    private void Run()
    {
        (Action<double> Write, double Value)[] presses;
        KeyValuePair<ControlMapping, (Action<double> Write, double Value)>[] values;

        lock (_waiting)
        {
            presses = _pressed.ToArray();
            values = _waiting.ToArray();

            _pressed.Clear();
            _waiting.Clear();

            _posted = false;
        }

        foreach (var press in presses)
        {
            try { press.Write(press.Value); }
            catch (Exception) { }
        }

        foreach (var (_, held) in values)
        {
            try { held.Write(held.Value); }
            catch (Exception) { }
        }
    }
}
