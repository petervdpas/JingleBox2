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

    /// <summary>The tracker this panel is standing in, or null on a panel with no song.</summary>
    private readonly ITrackerPanel? _tracker;

    public TrackInstrumentDesigner(
        int track,
        TrackerInstrument instrument,
        IInstrumentAudition audition,
        Action changed,
        IWaveformService? waveforms = null,
        ITrackerPanel? tracker = null,
        InstrumentLibrary? library = null)
    {
        Track = track;
        _instrument = instrument;
        _audition = audition;
        _changed = changed;
        _tracker = tracker;

        // The octave is one number the whole song shares, so a panel follows it rather than
        // holding a copy that would drift from the pattern editor's.
        if (tracker != null)
        {
            tracker.PropertyChanged += OnTrackerChanged;
            tracker.NotePlayed += OnTrackerNote;
        }

        Editor = new InstrumentEditorViewModel(track, instrument, changed, waveforms, audition);

        // A panel opened from a track can say where that track is. One opened without a tracker
        // still gets the lamps, with nothing behind them: they are greyed rather than removed,
        // so the panel is the same panel wherever it is opened.
        Location = new TrackLocationViewModel(tracker);

        // The shelf is where a sound starts, and it does not stop being that once an instrument
        // is standing in a track: every other OddSkilla on it is somewhere this one can go.
        Presets = new InstrumentPresets(instrument, Reloaded);

        // A kit lights its own pads, from the same set the keyboard reads.
        Editor?.Kit?.Follow(Sounding);
    }

    /// <summary>Which track this is the instrument of, for a title that says so.</summary>
    public int Track { get; }

    public InstrumentEditorViewModel? Editor { get; }

    /// <summary>The name for the window's title bar: the instrument, and where it is playing.</summary>
    public string Title => _instrument.Name + "  (track " + (Track + 1).ToString("00") + ")";

    /// <summary>
    /// Which octave this panel plays at, which is the song's octave when there is a song.
    /// </summary>
    /// <remarks>
    /// The pattern editor's octave field and these lamps are two views of one number. Moving
    /// it here moves it there, and the song remembers it. On the library page there is no song
    /// to remember it, so the panel keeps its own until the page is left.
    /// </remarks>
    public int Octave
    {
        get => _tracker?.Octave ?? _octave;
        set
        {
            int wanted = Math.Clamp(value, 0, 9);
            if (Octave == wanted) return;

            if (_tracker != null) _tracker.Octave = wanted;
            else _octave = wanted;

            OnPropertyChanged();
        }
    }

    private int _octave = 4;

    private void OnTrackerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Somebody moved it in the pattern editor. The lamps here follow.
        if (e.PropertyName is nameof(ITrackerPanel.Octave) or null) OnPropertyChanged(nameof(Octave));
    }

    [ObservableProperty] private int noteTrigger;

    [ObservableProperty] private double scopeCycles = 2;

    public double HoldSeconds => TrackerPlayer.PreviewHoldSeconds;

    public IRelayCommand TestCommand => new RelayCommand(Test);

    /// <summary>Somewhere to start: the shelf's other instruments on this same machine.</summary>
    public InstrumentPresets? Presets { get; }

    /// <summary>A preset has landed on the instrument, so the panel and the song both hear it.</summary>
    private void Reloaded()
    {
        Editor?.Reloaded();
        _changed();
    }

    /// <summary>Which notes are sounding, for the panel's keyboard to light.</summary>
    public SoundingNotes Sounding { get; } = new();

    public IRelayCommand<int> KeyCommand =>
        new RelayCommand<int>(semitone => Play(new Note(semitone), TrackerCell.NoVolume));

    /// <summary>
    /// A note went to a track. If it went to this one, the keyboard shows it.
    /// </summary>
    /// <remarks>
    /// Every track's notes come through here, which is why the first thing it does is throw
    /// away the ones belonging to somebody else. The note is struck alone, since a track has
    /// one voice: what it plays next puts out what it is playing now.
    /// </remarks>
    private void OnTrackerNote(object? sender, (int Track, Note Note) e)
    {
        if (e.Track != Track) return;

        Sounding.Struck(e.Note, HoldSeconds, alone: true);
        Reveal(e.Note);
    }

    /// <summary>
    /// Moves the keyboard's three octaves if a note landed outside them.
    /// </summary>
    /// <remarks>
    /// Through the tracker's own follow rather than by setting the octave, because a note the
    /// song played is not somebody editing the song. Set it here and every playback that went
    /// up an octave would leave the song asking to be saved.
    /// </remarks>
    private void Reveal(Note note)
    {
        int wanted = PanelKeyboard.Reveal(note, Octave);
        if (wanted == Octave) return;

        if (_tracker != null)
        {
            _tracker.FollowOctave(wanted);
            return;
        }

        _octave = wanted;
        OnPropertyChanged(nameof(Octave));
    }

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

        Sounding.Struck(note, HoldSeconds);
        Reveal(note);

        // The scopes draw themselves from this, so they follow what was just played.
        NoteTrigger++;
    }

    /// <summary>Lets go of the tracker, for a panel nobody can reach any more.</summary>
    public void Close()
    {
        if (_tracker != null)
        {
            _tracker.PropertyChanged -= OnTrackerChanged;
            _tracker.NotePlayed -= OnTrackerNote;
        }

        Sounding.Silence();
        Location?.Dispose();
    }

    /// <summary>The instrument's name may have been typed into; the strip shows it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        _changed();
    }
}
