using JingleBox2.ViewModels;
using System.Collections.ObjectModel;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// The pads, as far as a mapped button is concerned.
/// </summary>
/// <remarks>
/// The first of the three adapters, and the smallest: it turns a pad number into the pad's own
/// command, which is the same command the button on the screen runs. See
/// <see cref="TrackerNoteAdapter"/> for notes and <see cref="ControlTargets"/> for knobs.
///
/// It holds the live collection rather than a copy of it, because the number of pads is a
/// setting and the collection is rebuilt when it changes. A copy would go on firing pads that
/// are no longer on the screen.
/// </remarks>
public sealed class PadTriggerAdapter : IPadTrigger
{
    private readonly ObservableCollection<PadViewModel> _pads;

    /// <param name="pads">The live collection, since how many pads there are is a setting.</param>
    public PadTriggerAdapter(ObservableCollection<PadViewModel> pads)
    {
        _pads = pads;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Through the pad's own commands, so a button on the desk and a button on the screen take
    /// exactly the same path and there is no second place for the two to disagree about what
    /// playing a pad means.
    /// </remarks>
    public void TriggerPad(int padIndex, PadTriggerAction action)
    {
        if (padIndex < 0 || padIndex >= _pads.Count) return;

        var pad = _pads[padIndex];

        switch (action)
        {
            case PadTriggerAction.Toggle:
                pad.TogglePlayCommand.Execute(null);
                break;
            case PadTriggerAction.Start:
                pad.PlayCommand.Execute(null);
                break;
            case PadTriggerAction.Stop:
                pad.StopCommand.Execute(null);
                break;
        }
    }
}
