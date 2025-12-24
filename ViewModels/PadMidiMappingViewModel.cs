using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Midi;

namespace JingleBox2.ViewModels;

public sealed partial class PadMidiMappingViewModel : ObservableObject
{
    private readonly MidiMapping _model;

    [ObservableProperty]
    private bool isLearning;

    public PadMidiMappingViewModel(MidiMapping model)
    {
        _model = model;
    }

    public int PadIndex => _model.PadIndex;

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

    public MidiMapping ToModel() => _model;
}
