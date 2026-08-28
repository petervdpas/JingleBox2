using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using System;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument designer, pointed at the instrument one track is playing.
/// </summary>
/// <remarks>
/// The same panel the INSTRUMENTS tab shows, over a different instrument: the song's own copy
/// rather than the rack's. That is the whole point of it. A machine standing in this song's
/// rack is edited here, and what is edited is this song's, so two songs can use the same kick
/// sounding differently.
/// </remarks>
public sealed partial class TrackInstrumentDesigner : ObservableObject, IInstrumentDesigner
{
    /// <summary>
    /// The song's own copy of the instrument, which is what makes this panel different from the
    /// rack's: editing it here changes this song and no other.
    /// </summary>
    private readonly TrackerInstrument _instrument;

    /// <summary>
    /// How a note is sounded, which is the tracker's own engine rather than one of this
    /// panel's: a second engine would be a second output device and every plugin loaded twice.
    /// </summary>
    private readonly IInstrumentAudition _audition;

    /// <summary>Told after every edit, which is what marks the song unsaved.</summary>
    private readonly Action _changed;

    /// <summary>The tracker this panel is standing in, or null on a panel with no song.</summary>
    private readonly ITrackerPanel? _tracker;

    /// <summary>
    /// Builds the panel over one track's instrument and wires it to the song around it.
    /// </summary>
    /// <remarks>
    /// Everything after the first four is optional because the same panel opens on the rack
    /// page, where there is no song, no track and no tracker: what is missing there is greyed
    /// rather than absent, so the panel is the same panel wherever it is opened and you learn
    /// where things are once.
    /// </remarks>
    public TrackInstrumentDesigner(
        int track,
        TrackerInstrument instrument,
        IInstrumentAudition audition,
        Action changed,
        IWaveformService? waveforms = null,
        ITrackerPanel? tracker = null,
        MachineRack? rack = null,
        System.Collections.ObjectModel.ObservableCollection<JingleBox2.Models.Recording>? recordings = null,
        Midi.Interfaces.IMidiMonitor? keys = null)
    {
        Track = track;
        _keys = keys;
        _instrument = instrument;
        _audition = audition;
        _changed = changed;
        _tracker = tracker;

        if (tracker != null)
        {
            tracker.PropertyChanged += OnTrackerChanged;
            tracker.NotePlayed += OnTrackerNote;
        }

        Editor = new InstrumentEditorViewModel(
            track, instrument, changed, waveforms, audition, recordings, note => Play(note));

        Location = new TrackLocationViewModel(tracker);

        Presets = new InstrumentPresets(instrument, Reloaded, Editor?.Takes.Shown, Editor?.Takes);

        Editor?.Kit?.Follow(Sounding);

        Sounding.Ticked += MovePlayhead;
    }

    /// <summary>Which track this is the instrument of, for a title that says so.</summary>
    public int Track { get; }

    /// <summary>
    /// This panel's window came to the front, so its track is the one being worked on.
    /// </summary>
    /// <remarks>
    /// What "the track you are on" means is the instrument window in front when there is one,
    /// and the pattern cursor otherwise: two panels open in their own windows and the cursor is
    /// on neither of them, so a knob would drive whichever track the pattern last happened to
    /// be on. Nothing is applied by saying this; see <see cref="ITrackerPanel.PanelInFront"/>.
    /// </remarks>
    public void InFront() => _tracker?.PanelInFront(Track);

    /// <summary>And has gone, so the cursor says where you are again.</summary>
    public void NotInFront() => _tracker?.PanelGone(Track);

    /// <inheritdoc/>
    /// <remarks>Built once in the constructor, so it is never null on a panel over a track.</remarks>
    public InstrumentEditorViewModel? Editor { get; }

    /// <summary>The name for the window's title bar: the instrument, and where it is playing.</summary>
    public string Title => _instrument.Name + "  (track " + (Track + 1).ToString("00") + ")";

    /// <inheritdoc/>
    /// <remarks>
    /// The song's octave when there is a song. The pattern editor's octave field and these
    /// lamps are two views of one number: moving it here moves it there, and the song remembers
    /// it. On the rack page there is no song to remember it, so the panel keeps its own until
    /// the page is left. Held between nought and nine, which is as far as the lamps count.
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

    /// <summary>
    /// The octave a panel with no song keeps for itself, until the page is left. Unused while
    /// there is a tracker, which owns the one number the whole song shares.
    /// </summary>
    private int _octave = 4;

    /// <summary>
    /// Somebody moved the octave in the pattern editor, so the lamps here follow it. A null
    /// name means everything moved, which is what opening a song looks like.
    /// </summary>
    private void OnTrackerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ITrackerPanel.Octave) or null) OnPropertyChanged(nameof(Octave));
    }

    /// <inheritdoc cref="IInstrumentDesigner.NoteTrigger"/>
    /// <remarks>Moved on by every note this panel plays, and by nothing else.</remarks>
    [ObservableProperty] private int noteTrigger;

    /// <inheritdoc cref="IInstrumentDesigner.ScopeCycles"/>
    [ObservableProperty] private double scopeCycles = 2;

    /// <inheritdoc/>
    /// <remarks>
    /// The tracker's own preview hold, so a note played on a panel lasts exactly as long as one
    /// played on the pattern's keyboard. It is only used for a sound nobody knows the length
    /// of, which is anything generated: a recording lasts as long as the recording.
    /// </remarks>
    public double HoldSeconds => TrackerPlayer.PreviewHoldSeconds;

    /// <inheritdoc/>
    /// <remarks>Plays C at the panel's octave, which is the note the scopes are drawn from.</remarks>
    public IRelayCommand TestCommand => new RelayCommand(Test);

    /// <inheritdoc/>
    /// <remarks>
    /// Built over the song's own copy of the instrument, so picking one off the shelf lands on
    /// this song and leaves the rack's untouched.
    /// </remarks>
    public InstrumentPresets? Presets { get; }

    /// <summary>A preset has landed on the instrument, so the panel and the song both hear it.</summary>
    private void Reloaded()
    {
        Editor?.Reloaded();
        _changed();
    }

    /// <summary>Where the sound has got to, for the cursor over the picture.</summary>
    /// <remarks>
    /// Both pictures read the same number: the one whole recording a sampler shows, and the
    /// pieces a chopped one shows. A machine has one or the other, never both, so this sets
    /// whichever is there.
    ///
    /// Nothing lit is nothing sounding, and that is the last beat of the clock before it stops.
    /// Asking the engine at that moment would catch a voice still letting go of its release and
    /// leave the line standing in the middle of the picture with nothing playing, so the cursor
    /// is taken off instead.
    /// </remarks>
    private void MovePlayhead()
    {
        var editor = Editor;

        if (editor == null) return;

        double at = Sounding.Lit.Count == 0 ? -1 : _audition.SamplePosition(Track);

        editor.Playhead = at;

        if (editor.Slices != null) editor.Slices.Playhead = at;
    }

    /// <inheritdoc/>
    public SoundingNotes Sounding { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Goes to the song's own copy of the instrument, not to the rack's.</remarks>
    public void Let(Note note) => _audition.Let(_instrument, note);

    /// <inheritdoc/>
    /// <remarks>Built the first time a machine asks for it, since most panels never do.</remarks>
    public IMachineKeys MachineKeys => _machineKeys ??= new DesignerKeys(this);

    /// <summary>Kept so it can be let go of in <see cref="Close"/>.</summary>
    private IMachineKeys? _machineKeys;

    /// <inheritdoc/>
    /// <remarks>
    /// Handed in and passed on untouched. A panel that counted the presses it had heard itself
    /// showed nothing for a key on the hardware, since that key never touches a panel: it goes
    /// to whoever the notes are being played on.
    /// </remarks>
    public Midi.Interfaces.IMidiMonitor? MidiKeys => _keys;

    /// <summary>The monitor, or null on a panel built without one.</summary>
    private readonly Midi.Interfaces.IMidiMonitor? _keys;


    /// <summary>
    /// A note went to a track. If it went to this one, the keyboard shows it.
    /// </summary>
    /// <remarks>
    /// Every track's notes come through here, which is why the first thing it does is throw
    /// away the ones belonging to somebody else. The note is struck alone, since a track has
    /// one voice: what it plays next puts out what it is playing now.
    ///
    /// It is lit for as long as it will sound where that is known, and for the usual moment
    /// where it is not, which is anything generated.
    /// </remarks>
    private void OnTrackerNote(object? sender, (int Track, Note Note, double Seconds) e)
    {
        if (e.Track != Track) return;

        Sounding.Struck(e.Note, e.Seconds > 0 ? e.Seconds : HoldSeconds, alone: true);
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

    /// <inheritdoc/>
    /// <remarks>
    /// Always built, even without a tracker: the lamps are then greyed rather than taken off,
    /// so the panel is the same panel wherever it is opened.
    /// </remarks>
    public TrackLocationViewModel? Location { get; }

    /// <inheritdoc/>
    /// <remarks>False on the rack page, where the lamps are drawn greyed rather than removed.</remarks>
    public bool HasLocation => Location?.IsLive == true;

    /// <inheritdoc/>
    /// <remarks>Built the first time a described face asks for it, since most do not.</remarks>
    public Machines.Interfaces.IMachineLocation? MachineLocation =>
        _place ??= Location is { } place ? new Tracker.Machines.TrackLocation(place) : null;

    /// <summary>Built the first time a machine's face asks for the lamps.</summary>
    private Machines.Interfaces.IMachineLocation? _place;

    /// <summary>Plays C at the panel's own octave, which is what the TEST cap does.</summary>
    private void Test() => Play(Note.FromOctave(0, Octave));

    /// <inheritdoc/>
    /// <remarks>
    /// Sounds the instrument as it is now, so a knob just turned can be heard.
    ///
    /// Through the same audition the rack uses, which is the tracker's own engine. A second
    /// engine would be a second output device and a plugin loaded twice.
    ///
    /// The key is lit for as long as the sound lasts, which for a recording is the recording's
    /// own length and for anything generated is the usual moment. The scopes draw themselves
    /// from <see cref="NoteTrigger"/>, so moving it on is what makes them follow what was
    /// just played.
    /// </remarks>
    public void Play(Note note, int volume = TrackerCell.NoVolume)
    {
        if (!note.IsPlayable) return;

        double held = _audition.Audition(_instrument, note, volume);

        Sounding.Struck(note, held > 0 ? held : HoldSeconds);
        Reveal(note);

        NoteTrigger++;
    }

    /// <summary>
    /// Lets go of the tracker, for a panel nobody can reach any more.
    /// </summary>
    /// <remarks>
    /// The monitor and the tracker both outlive this window, so everything hung on them has to
    /// come off: a panel left listening would go on lighting keys nobody can see, and would
    /// keep the window it belongs to alive with it.
    /// </remarks>
    public void Close()
    {
        if (_tracker != null)
        {
            _tracker.PropertyChanged -= OnTrackerChanged;
            _tracker.NotePlayed -= OnTrackerNote;
        }

        Sounding.Silence();
        Location?.Dispose();

        (_machineKeys as IDisposable)?.Dispose();
    }

    /// <summary>The instrument's name may have been typed into; the strip shows it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        _changed();
    }
}
