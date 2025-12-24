using JingleBox2.ViewModels;
using System.Collections.ObjectModel;

namespace JingleBox2.Midi;

public sealed class PadTriggerAdapter : IPadTrigger
{
    private readonly ObservableCollection<PadViewModel> _pads;

    public PadTriggerAdapter(ObservableCollection<PadViewModel> pads)
    {
        _pads = pads;
    }

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
