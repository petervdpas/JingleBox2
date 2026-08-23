using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

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
    /// Which notes are sounding, for the panel's keyboard to light.
    /// </summary>
    /// <remarks>
    /// Notes played by hand, and on a panel standing in a track, the notes the pattern plays
    /// on that track as well. Between this and the LOCATION lamps a panel says where its track
    /// is and what it is doing there.
    /// </remarks>
    SoundingNotes Sounding { get; }

    /// <summary>Plays a key on the panel's keyboard, named by its absolute semitone.</summary>
    IRelayCommand<int> KeyCommand { get; }

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
}
