using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;

namespace JingleBox2.ViewModels;

/// <summary>
/// One row of the pad mapping dialog: a button on a controller, pointed at a pad.
/// </summary>
/// <remarks>
/// A thin coat over <see cref="MidiMapping"/> rather than a copy of it. The settings hold the
/// mapping itself, so every setter here writes through to the one the router reads and
/// <see cref="ToModel"/> hands back the same object it was given: a view model that kept its
/// own copy would have the dialog and the router disagreeing about a button until something
/// saved.
/// </remarks>
public sealed partial class PadMidiMappingViewModel : ObservableObject
{
    /// <summary>The mapping in the settings, written through rather than copied.</summary>
    private readonly MidiMapping _model;

    /// <summary>
    /// True while this row is waiting for the next message from the desk to fill itself in.
    /// </summary>
    /// <remarks>
    /// One row at a time: whoever turns this on is responsible for turning it off on the rows
    /// beside it, since two rows learning at once would both take the same press.
    /// </remarks>
    [ObservableProperty]
    private bool isLearning;

    /// <summary>Shows an existing mapping, or a fresh one for a pad nothing points at yet.</summary>
    public PadMidiMappingViewModel(MidiMapping model)
    {
        _model = model;
    }

    /// <summary>Which pad this row is about, counted from nought. Fixed once the row exists.</summary>
    public int PadIndex => _model.PadIndex;

    /// <summary>A note or a controller, which is what the hardware sends when the button is hit.</summary>
    public MidiMessageType Type
    {
        get => _model.Type;
        set
        {
            if (_model.Type == value) return;
            _model.Type = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The channel, 1 to 16 as the message says it.
    /// </summary>
    /// <remarks>
    /// Held inside the range rather than refused: this is typed into and learned from hardware,
    /// and a nought that the wire cannot carry is a row that quietly matches nothing.
    /// </remarks>
    public int Channel
    {
        get => _model.Channel;
        set
        {
            var v = value < 1 ? 1 : (value > 16 ? 16 : value);
            if (_model.Channel == v) return;
            _model.Channel = v;
            OnPropertyChanged();
        }
    }

    /// <summary>The note number, or the controller number, depending on <see cref="Type"/>.</summary>
    public int Value
    {
        get => _model.Value;
        set
        {
            if (_model.Value == value) return;
            _model.Value = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The mapping this row has been editing, for the settings to write down.
    /// </summary>
    /// <remarks>
    /// The same instance that was handed in, not a new one built from the properties: the row
    /// has been writing through all along, so there is nothing left to copy back.
    /// </remarks>
    public MidiMapping ToModel() => _model;
}
