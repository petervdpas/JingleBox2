using CommunityToolkit.Mvvm.Input;
using JingleBox2.Rack.Machines.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// What the instrument designer needs around it: the instrument being worked on, and the means
/// to hear it while you work.
/// </summary>
/// <remarks>
/// The designer is one control shown in two places: on the INSTRUMENTS tab against whatever the
/// rack has picked, and in a window of its own against the instrument a track plays. This is
/// the small surface both of those have to offer, so neither has to be the other.
///
/// Hearing it is part of it. A wave you cannot play is a picture, so the octave to test at, the
/// command that plays it, and the two numbers the scopes draw themselves from all live here
/// rather than on whichever page happens to be hosting the panel.
/// </remarks>
public interface IInstrumentDesigner
{
    /// <summary>The instrument being worked on, or null when nothing is picked.</summary>
    InstrumentEditorViewModel? Editor { get; }

    /// <summary>Which octave the test note plays at.</summary>
    int Octave { get; set; }

    /// <summary>Bumped every time a note is played, so the scopes know to redraw.</summary>
    int NoteTrigger { get; }

    /// <summary>How many cycles of the wave the shape scope draws.</summary>
    double ScopeCycles { get; set; }

    /// <summary>How long a test note is held, so the envelope scope draws the right sustain.</summary>
    double HoldSeconds { get; }

    /// <summary>Plays the instrument, so what has just been changed can be heard.</summary>
    IRelayCommand TestCommand { get; }

    /// <summary>
    /// Plays one note on the instrument, for the panel's own keyboard.
    /// </summary>
    /// <remarks>
    /// The panel plays wherever it is opened. A knob is easier to judge while you are playing
    /// than one test note at a time, and an instrument standing in a track has no test button
    /// at all, so without this the window is silent until the pattern reaches that row.
    /// </remarks>
    void Play(Note note, int volume);

    /// <summary>
    /// Lets go of one note played by hand: the same thing a pattern's OFF does to a track.
    /// </summary>
    /// <remarks>
    /// A key is down while a hand is on it and up when the hand comes off. What it started goes
    /// into its release then, rather than running to the end of the file, which is what makes a
    /// keyboard on a panel behave like the keys in a pattern.
    /// </remarks>
    void Let(Note note);

    /// <summary>
    /// Which notes are sounding, for the panel's keyboard to light.
    /// </summary>
    /// <remarks>
    /// Notes played by hand, and on a panel standing in a track, the notes the pattern plays
    /// on that track as well. Between this and the LOCATION lamps a panel says where its track
    /// is and what it is doing there.
    /// </remarks>
    SoundingNotes Sounding { get; }

    /// <summary>
    /// A key on a panel's keyboard going down, and coming up again.
    /// </summary>
    /// <remarks>
    /// The only way a keyboard plays anything. Both go through <see cref="MachineKeys"/>, which
    /// is the one place that knows which keys a hand is on: the held set, the guard against a
    /// held key repeating, and the note-off all live there and exist once.
    ///
    /// There used to be a second way, a plain command that played a note and said nothing about
    /// letting go. A keyboard wired to it lit from the sounding notes instead of from the hand,
    /// so it lagged behind by however long the sound lasted, and the two keyboards on the same
    /// page behaved differently.
    /// </remarks>
    IRelayCommand<int> KeyPressCommand => new RelayCommand<int>(MachineKeys.Play);

    /// <inheritdoc cref="KeyPressCommand"/>
    IRelayCommand<int> KeyLetCommand => new RelayCommand<int>(MachineKeys.Let);

    /// <summary>
    /// The keyboard on a machine's own face, when the machine draws one.
    /// </summary>
    /// <remarks>
    /// The same three things the keyboard at the foot of the panel was bound to, offered as one
    /// object because a panel drawn from a description has nothing to bind with. It answers more
    /// than that one did: which keys have something on them and which one is in hand, neither of
    /// which the shared keyboard could say because it did not know what it was standing under.
    /// </remarks>
    IMachineKeys MachineKeys { get; }

    /// <summary>
    /// Which keys are down, from every producer, or nothing for a panel standing on its own.
    /// </summary>
    /// <remarks>
    /// One of these for the application, wired to the note stream at startup. A panel is handed
    /// it rather than watching the notes itself, because what a keyboard shows has nothing to do
    /// with which panel heard which note.
    /// </remarks>
    Midi.Interfaces.IMidiMonitor? MidiKeys => null;

    /// <summary>
    /// Somewhere to start: the instruments the shelf already holds on this same machine.
    /// </summary>
    /// <remarks>
    /// Null when there is no shelf to read, which greys the picker rather than removing it.
    /// </remarks>
    InstrumentPresets? Presets { get; }


    /// <summary>
    /// Where the track playing this instrument has got to, or null when there is no track.
    /// </summary>
    /// <remarks>
    /// The rack page edits an instrument nothing is playing, so it has no location to show
    /// and the lamps stay off the panel entirely rather than sitting there dark.
    /// </remarks>
    TrackLocationViewModel? Location { get; }

    /// <summary>True when there is a location worth putting on the panel.</summary>
    bool HasLocation { get; }

    /// <summary>
    /// The same thing again, in the words a machine's own face understands.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="Location"/> rather than instead of it while both panels exist: the
    /// blocks written in XAML bind to the view model, and a described panel is handed this.
    /// Never null, because the row is drawn dimmed where nothing is playing rather than taken
    /// off a panel that has asked for it.
    /// </remarks>
    IMachineLocation? MachineLocation { get; }
}
