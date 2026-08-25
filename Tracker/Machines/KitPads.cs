using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// A kit, shown to a described panel as the grid of pads on its face.
/// </summary>
/// <remarks>
/// The kit already exists and is already being edited: <see cref="DrumKitViewModel"/> is what
/// the pads have always been bound to. This is the same kit answering the questions the panel
/// asks, which are fewer and simpler, because a panel drawn from a description knows nothing
/// about drums.
///
/// Nothing is copied. Two views of one kit that each held their own list would disagree the
/// first time a note was played, and what is being watched here moves on every note.
/// </remarks>
public sealed class KitPads(DrumKitViewModel kit) : IMachinePads
{
    private bool _listening;

    public int Count => kit.Pads.Count;

    public string Cap(int at) => Pad(at)?.CapText ?? "";

    public string Note(int at) => Pad(at)?.NoteText ?? "";

    public bool Lit(int at) => Pad(at)?.IsLit ?? false;

    public bool Filled(int at) => Pad(at)?.HasSound ?? false;

    /// <summary>Which pad the strip of controls beside the grid is about.</summary>
    public int Picked
    {
        get => kit.Selected is { } one ? kit.Pads.IndexOf(one) : -1;
        set => kit.SelectAt(value);
    }

    /// <summary>Hits it, which is what pressing a pad has always done.</summary>
    public void Hit(int at) => Pad(at)?.TapCommand.Execute(null);

    /// <summary>
    /// Told when a pad lights, is picked, or is given a different recording.
    /// </summary>
    /// <remarks>
    /// Wired on the first listener rather than in the constructor, so a kit nothing is watching
    /// costs nothing. The pads are watched as well as the kit: a pad lighting is a change to the
    /// pad, and the kit says nothing when one does.
    /// </remarks>
    public event EventHandler? Changed
    {
        add
        {
            _changed += value;

            Listen();
        }
        remove => _changed -= value;
    }

    private EventHandler? _changed;

    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        kit.PropertyChanged += Moved;

        foreach (var pad in kit.Pads) pad.PropertyChanged += Moved;

        // A kit refilled from a chop is a new set of pads, so the new ones are listened to and
        // the old ones stop mattering along with the objects they were.
        kit.Pads.CollectionChanged += (_, _) =>
        {
            foreach (var pad in kit.Pads)
            {
                pad.PropertyChanged -= Moved;
                pad.PropertyChanged += Moved;
            }

            Moved(null, new PropertyChangedEventArgs(nameof(kit.Pads)));
        };
    }

    private void Moved(object? sender, PropertyChangedEventArgs e) => _changed?.Invoke(this, EventArgs.Empty);

    private DrumPadViewModel? Pad(int at) => kit.Pads.ElementAtOrDefault(at);
}
