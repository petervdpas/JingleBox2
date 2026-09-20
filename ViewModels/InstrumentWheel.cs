using System;
using System.Collections.Generic;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>
/// Which of its machine's controls one instrument's modulation wheel turns.
/// </summary>
/// <remarks>
/// The lines under a device's Menu, and the one place an instrument's
/// <see cref="TrackerInstrument.WheelKey"/> is written. It exists because there was no way to
/// change what the wheel does without a keyboard: a link made by pointing is filled in from a
/// MIDI message, so a drawn wheel, which sends none, could never be pointed at anything, and the
/// machine's own declaration was the only answer anybody had.
///
/// **The first row is the machine's own and is what empty means**, which is why it is a row
/// rather than a blank: it is the row somebody picks to put the wheel back where the machine
/// meant it, and it is what is marked on an instrument nobody has asked about. Picking it again
/// writes nothing, so an instrument that has never been touched here is written down exactly as
/// it was.
///
/// The rows after it are the controls on the machine's own face, in the order it draws them. What is
/// written down is the control's **key**: a machine that grows a control in the middle of its
/// face still means the same thing by the key it already had, where a row number would quietly
/// move the wheel onto its neighbour.
///
/// The names are what a person reads on the face and the keys are what the song holds, which is
/// the split <c>Parameter</c> already keeps. A control with no key of its own is not offered,
/// since there would be nothing to write down for it.
/// </remarks>
/// <param name="instrument">Whose wheel it is.</param>
/// <param name="machine">The machine it is on, asked for each time; nothing where it is not registered here.</param>
/// <param name="changed">Told when the choice moves, so the song can be marked as having something unsaved in it.</param>
public sealed class InstrumentWheel(TrackerInstrument instrument,
                                    Func<SoundMachineProject?> machine,
                                    Action? changed = null) : IInstrumentWheel
{
    /// <summary>What the first row says, which is the machine deciding.</summary>
    /// <remarks>
    /// A sentence rather than a key, since it is the only row that is not one, and it has to read
    /// as an answer rather than as a control called something odd.
    /// </remarks>
    public const string AsTheMachineSays = "as the machine says";

    /// <inheritdoc/>
    public IReadOnlyList<string> Names
    {
        get
        {
            var names = new List<string> { AsTheMachineSays };

            foreach (var parameter in Keys()) names.Add(Called(parameter));

            return names;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nought is the machine's own, which is what an instrument that has said nothing reads back
    /// as. A key the machine no longer has answers -1, so the picker shows nothing rather than
    /// the first control on the face, which would be a lie about what the wheel is doing.
    /// </remarks>
    public int Picked
    {
        get
        {
            if (instrument.WheelKey is not { Length: > 0 } key) return 0;

            var keys = Keys();

            for (int at = 0; at < keys.Count; at++)
            {
                if (string.Equals(keys[at].Key, key, StringComparison.OrdinalIgnoreCase)) return at + 1;
            }

            return -1;
        }

        set
        {
            var keys = Keys();

            string wanted = value >= 1 && value - 1 < keys.Count ? keys[value - 1].Key : "";

            if (string.Equals(instrument.WheelKey, wanted, StringComparison.Ordinal)) return;

            instrument.WheelKey = wanted;

            changed?.Invoke();
        }
    }

    /// <summary>
    /// The machine's controls a wheel could turn, in the order its face draws them.
    /// </summary>
    /// <remarks>
    /// **Read off the face and not off the manifest's list**, which is the same walk the
    /// automation picker makes and for the same two reasons. The order a machine happens to
    /// declare its parameters in is not an order anybody sees, where the face is: the third row
    /// here is the third control your eye lands on. And a parameter with no control on the face
    /// is plumbing rather than a choice, so it is not offered: there would be nothing to watch
    /// move.
    ///
    /// A parameter that is not kept is left out too. A wheel writes a value, and a value that is
    /// not written down is one that has gone by the next time the song is opened.
    ///
    /// Read each time rather than kept, so a machine edited between two openings offers what it
    /// has now.
    /// </remarks>
    private IReadOnlyList<Rack.SoundDevices.Faces.Parameter> Keys()
    {
        if (machine() is not { } project) return Array.Empty<Rack.SoundDevices.Faces.Parameter>();

        var byKey = new Dictionary<string, Rack.SoundDevices.Faces.Parameter>(StringComparer.Ordinal);

        foreach (var parameter in project.Parameters)
        {
            if (parameter?.Key is { Length: > 0 } key) byKey[key] = parameter;
        }

        var keys = new List<Rack.SoundDevices.Faces.Parameter>();

        foreach (string key in _order.Of(project.Panel))
        {
            if (byKey.TryGetValue(key, out var parameter) && parameter.Saved) keys.Add(parameter);
        }

        return keys;
    }

    /// <summary>The order a face reads in, which is what the rows are offered in.</summary>
    private readonly Rack.SoundDevices.Faces.Interfaces.IPanelOrder _order =
        new Rack.SoundDevices.Faces.PanelOrder();

    /// <summary>What a row says: the control's own name, or its key where it has never been given one.</summary>
    private static string Called(Rack.SoundDevices.Faces.Parameter parameter) =>
        parameter.Name is { Length: > 0 } name ? name : parameter.Key;
}
