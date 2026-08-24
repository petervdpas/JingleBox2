using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// The pads, as the transport sees them: one button, and it stops everything.
/// </summary>
/// <remarks>
/// A jingle desk needs somewhere to put its hand when the wrong thing is on air, and this is
/// it. The other three caps stay greyed on purpose. There is no such thing as playing "the
/// pads": a pad is fired by its own pad, by a key, or by a MIDI note, and a transport that
/// could start one would mean a space bar that can put a jingle to air.
/// </remarks>
public sealed class PadDeck : ObservableObject, ITransportDeck
{
    private readonly ObservableCollection<PadViewModel> _pads;

    public PadDeck(ObservableCollection<PadViewModel> pads)
    {
        _pads = pads;

        _pads.CollectionChanged += OnPadsChanged;

        foreach (var pad in _pads) Watch(pad);
    }

    /// <summary>True while any pad is sounding.</summary>
    public bool IsRunning => _pads.Any(p => p.IsPlaying);

    public bool IsRecording => false;
    public bool IsPlaying => IsRunning;
    public bool IsPaused => false;

    public bool CanRecord => false;
    public bool CanPlay => false;
    public bool CanPause => false;

    public void Record() { }
    public void Play() { }
    public void Pause() { }

    /// <summary>Everything off. Pads that are not playing are left alone.</summary>
    public void Stop()
    {
        foreach (var pad in _pads.Where(p => p.IsPlaying).ToList())
            pad.StopCommand.Execute(null);
    }

    private void OnPadsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The matrix can be resized, which builds a new set of pads.
        foreach (var pad in _pads) Watch(pad);

        Changed();
    }

    private void Watch(PadViewModel pad)
    {
        pad.PropertyChanged -= OnPadChanged;
        pad.PropertyChanged += OnPadChanged;
    }

    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PadViewModel.IsPlaying)) Changed();
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPlaying));
    }
}
