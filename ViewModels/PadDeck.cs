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
    /// <summary>
    /// The pads themselves, held as the live collection rather than as a copy.
    /// </summary>
    /// <remarks>
    /// The matrix is a setting, so the collection is rebuilt while the application is running
    /// and anything holding a snapshot of it would be reporting on the last set of pads.
    /// </remarks>
    private readonly ObservableCollection<PadViewModel> _pads;

    /// <summary>
    /// Watches that collection and every pad in it, so the caps follow what is sounding.
    /// </summary>
    public PadDeck(ObservableCollection<PadViewModel> pads)
    {
        _pads = pads;

        _pads.CollectionChanged += OnPadsChanged;

        foreach (var pad in _pads) Watch(pad);
    }

    /// <inheritdoc/>
    /// <remarks>True while any pad is sounding, which is the only thing FIRE can be doing.</remarks>
    public bool IsRunning => _pads.Any(p => p.IsPlaying);

    /// <inheritdoc/>
    /// <remarks>Nothing records on this page, so this is always false.</remarks>
    public bool IsRecording => false;

    /// <inheritdoc/>
    /// <remarks>The same question as <see cref="IsRunning"/>: a pad is playing or it is not.</remarks>
    public bool IsPlaying => IsRunning;

    /// <inheritdoc/>
    /// <remarks>A pad has no pause, so this is always false.</remarks>
    public bool IsPaused => false;

    /// <inheritdoc/>
    /// <remarks>False, so the cap is greyed rather than being one that quietly does nothing.</remarks>
    public bool CanRecord => false;

    /// <inheritdoc/>
    /// <remarks>
    /// False deliberately. A pad is fired by its own pad, by a key or by a MIDI note, and there
    /// is no answer to "play the pads": the transport would have to pick one.
    /// </remarks>
    public bool CanPlay => false;

    /// <inheritdoc/>
    /// <remarks>False: nothing here can be paused and resumed where it left off.</remarks>
    public bool CanPause => false;

    /// <inheritdoc/>
    /// <remarks>Does nothing, and the cap that would call it is greyed.</remarks>
    public void Record() { }

    /// <inheritdoc/>
    /// <remarks>Does nothing, and the cap that would call it is greyed.</remarks>
    public void Play() { }

    /// <inheritdoc/>
    /// <remarks>Does nothing, and the cap that would call it is greyed.</remarks>
    public void Pause() { }

    /// <inheritdoc/>
    /// <remarks>
    /// The one cap that works here. Pads that are not playing are left alone, and the list is
    /// copied first because stopping a pad can take it out of what is being walked.
    /// </remarks>
    public void Stop()
    {
        foreach (var pad in _pads.Where(p => p.IsPlaying).ToList())
            pad.StopCommand.Execute(null);
    }

    /// <summary>
    /// Picks up a new set of pads after the matrix was resized.
    /// </summary>
    /// <remarks>
    /// Resizing builds a fresh set, so every pad is watched again rather than only the ones the
    /// event says arrived. <see cref="Watch"/> unsubscribes before it subscribes, so a pad that
    /// survived the resize is still heard exactly once.
    /// </remarks>
    private void OnPadsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var pad in _pads) Watch(pad);

        Changed();
    }

    /// <summary>
    /// Listens to one pad, taking the old subscription off first so it cannot be heard twice.
    /// </summary>
    private void Watch(PadViewModel pad)
    {
        pad.PropertyChanged -= OnPadChanged;
        pad.PropertyChanged += OnPadChanged;
    }

    /// <summary>
    /// A pad said something. Only whether it is sounding is any of the transport's business.
    /// </summary>
    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PadViewModel.IsPlaying)) Changed();
    }

    /// <summary>
    /// Tells the transport to read the deck again. Both names, since they answer separately
    /// and the caps bind to both.
    /// </summary>
    private void Changed()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPlaying));
    }
}
