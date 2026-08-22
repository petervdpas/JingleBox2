using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Tracker;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument designer, pointed at the instrument one track is playing.
/// </summary>
/// <remarks>
/// The same panel the INSTRUMENTS tab shows, over a different instrument: the song's own copy
/// rather than the library's. That is the whole point of it. A machine standing in this song's
/// rack is edited here, and what is edited is this song's, so two songs can use the same kick
/// sounding differently.
/// </remarks>
public sealed partial class TrackInstrumentDesigner : ObservableObject, IInstrumentDesigner
{
    private readonly TrackerInstrument _instrument;
    private readonly IInstrumentAudition _audition;
    private readonly Action _changed;

    public TrackInstrumentDesigner(
        int track,
        TrackerInstrument instrument,
        IInstrumentAudition audition,
        Action changed,
        IWaveformService? waveforms = null,
        ITrackerLocation? location = null)
    {
        Track = track;
        _instrument = instrument;
        _audition = audition;
        _changed = changed;

        Editor = new InstrumentEditorViewModel(track, instrument, changed, waveforms, audition);

        // A panel opened from a track can say where that track is. One opened without a tracker
        // still gets the lamps, with nothing behind them: they are greyed rather than removed,
        // so the panel is the same panel wherever it is opened.
        Location = new TrackLocationViewModel(location);
    }

    /// <summary>Which track this is the instrument of, for a title that says so.</summary>
    public int Track { get; }

    public InstrumentEditorViewModel? Editor { get; }

    /// <summary>The name for the window's title bar: the instrument, and where it is playing.</summary>
    public string Title => _instrument.Name + "  (track " + (Track + 1).ToString("00") + ")";

    [ObservableProperty] private int octave = 4;

    [ObservableProperty] private int noteTrigger;

    [ObservableProperty] private double scopeCycles = 2;

    public double HoldSeconds => TrackerPlayer.PreviewHoldSeconds;

    public IRelayCommand TestCommand => new RelayCommand(Test);

    public IRelayCommand OctaveDownCommand => new RelayCommand(() => Octave = Math.Max(0, Octave - 1));

    public IRelayCommand OctaveUpCommand => new RelayCommand(() => Octave = Math.Min(9, Octave + 1));

    /// <summary>Where the track playing this instrument has got to, for the LOCATION lamps.</summary>
    public TrackLocationViewModel? Location { get; }

    public bool HasLocation => Location?.IsLive == true;

    private void Test() => Play(Note.FromOctave(0, Octave));

    /// <summary>
    /// Sounds the instrument as it is now, so a knob just turned can be heard.
    /// </summary>
    /// <remarks>
    /// Through the same audition the library uses, which is the tracker's own engine. A second
    /// engine would be a second output device and a plugin loaded twice.
    /// </remarks>
    public void Play(Note note, int volume = TrackerCell.NoVolume)
    {
        if (!note.IsPlayable) return;

        _audition.Audition(_instrument, note, volume);

        // The scopes draw themselves from this, so they follow what was just played.
        NoteTrigger++;
    }

    /// <summary>Lets go of the tracker, for a window being closed.</summary>
    public void Close() => Location?.Dispose();

    /// <summary>The instrument's name may have been typed into; the strip shows it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        _changed();
    }
}
