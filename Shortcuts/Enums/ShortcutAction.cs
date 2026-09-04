namespace JingleBox2.Shortcuts.Enums;

/// <summary>
/// The things a keystroke can ask for, as a closed list rather than a name to spell.
/// </summary>
/// <remarks>
/// An enum and not a string, so the set of them is visible in one place and a page cannot ask
/// for something nothing offers. Adding one is adding a member here and a line in
/// <see cref="ShortcutActions.Everything"/>, and every page that does not answer it simply says it
/// cannot.
///
/// **Two kinds, and the difference is who decides.** The first four are the system's: what they
/// do is a fact about the application, every page answers them for itself, and they are not
/// yours to move. The rest are a page along the top, they ship on no key at all, and putting one
/// on a key is the whole of what the shortcuts page in SETTINGS is for.
///
/// The names are what a settings file holds, so they do not change once written down. The order
/// does not matter for that reason, which is why a page's word rather than a verb is the right
/// name here: a key pointed at TRACKER is asking for TRACKER.
/// </remarks>
public enum ShortcutAction
{
    /// <summary>Write down whatever the page in front of you owns: a song, a machine, the pads.</summary>
    Save,

    /// <summary>Take away whatever is picked out, on a page that has something to pick out.</summary>
    Delete,

    /// <summary>
    /// The last thing that was done on this page, put back.
    /// </summary>
    /// <remarks>
    /// There is no undo for the application: each page keeps its own, because what the last
    /// thing you did was is a question only the page you did it on can answer.
    /// </remarks>
    Undo,

    /// <summary>And the last thing undone, done again.</summary>
    Redo,

    /// <summary>The mixer.</summary>
    Mixer,

    /// <summary>RECORD, where the takes are made.</summary>
    Record,

    /// <summary>PADS, where they are laid out.</summary>
    Pads,

    /// <summary>FIRE, where they are played.</summary>
    Fire,

    /// <summary>TRACKER, the song and the rack beside it.</summary>
    Tracker,

    /// <summary>DESIGNER, which is only there when it has been asked for in SETTINGS.</summary>
    Designer,

    /// <summary>SETTINGS.</summary>
    Settings,

    /// <summary>MIDI CC, the control templates.</summary>
    MidiCc
}
