using JingleBox2.Machines;
using JingleBox2.Tracker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// The keyboard on a machine's own face, answered by whoever is showing the panel.
/// </summary>
/// <remarks>
/// The keyboard was the app's for as long as it was the same on every machine: one at the foot
/// of the panel, playing whatever was in front of it. On a kit it is not the same. Sixteen keys
/// out of a hundred and twenty do anything, and which key fires which drum is the question the
/// keyboard is there to answer, so the machine puts one on its own face and this is what stands
/// behind it.
///
/// Two pictures of one thing. The pads and the keys are the same sixteen drums, so picking a pad
/// outlines its key and hitting a key lights its pad, and neither of those is arranged anywhere:
/// they are the same fact read twice.
///
/// It follows rather than copies. What is sounding is the designer's own set, what is on the
/// pads is the kit's own list, and this holds neither: a second copy would be wrong the first
/// time a note was played.
/// </remarks>
public sealed class DesignerKeys : IMachineKeys
{
    private readonly IInstrumentDesigner _designer;

    private DrumKitViewModel? _watching;

    public DesignerKeys(IInstrumentDesigner designer)
    {
        _designer = designer;

        // The notes sounding move on every note, and the keys light off them.
        _designer.Sounding.Lit.CollectionChanged += Moved;
    }

    /// <summary>What is sounding this instant.</summary>
    public IEnumerable Lit => _designer.Sounding.Lit;

    /// <summary>
    /// The keys with something on them, which on anything but a kit is none of them.
    /// </summary>
    /// <remarks>
    /// A machine that answers every key answers with nothing here, because a keyboard with all
    /// hundred and twenty keys banded says exactly as much as one with none.
    /// </remarks>
    public IEnumerable Filled
    {
        get
        {
            Follow();

            if (Kit is not { } kit) return Array.Empty<int>();

            return kit.Pads.Where(pad => pad.HasSound).Select(pad => pad.Semitone).ToList();
        }
    }

    /// <summary>The key belonging to the pad in hand, so the grid and the keyboard agree.</summary>
    public int Marked
    {
        get
        {
            Follow();

            return Kit?.Selected?.Semitone ?? -1;
        }
    }

    /// <summary>Where the keyboard is looking, which is the designer's own and not the song's.</summary>
    public int Octave
    {
        get => _designer.Octave;
        set
        {
            if (_designer.Octave == value) return;

            _designer.Octave = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Plays it, through the same command the keyboard at the foot of the panel used.</summary>
    public void Play(int semitone) => _designer.KeyCommand.Execute(semitone);

    public event EventHandler? Changed;

    private DrumKitViewModel? Kit => _designer.Editor?.Kit;

    /// <summary>
    /// Listens to whichever kit is in front of it now.
    /// </summary>
    /// <remarks>
    /// Asked on the way past rather than wired up once, because the instrument being edited
    /// changes under this and the kit changes with it. Picking a different pad is a change to
    /// the kit and nothing else says so.
    /// </remarks>
    private void Follow()
    {
        var kit = Kit;

        if (ReferenceEquals(kit, _watching)) return;

        if (_watching != null) _watching.PropertyChanged -= Moved;

        _watching = kit;

        if (_watching != null) _watching.PropertyChanged += Moved;
    }

    private void Moved(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void Moved(object? sender, NotifyCollectionChangedEventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);
}
