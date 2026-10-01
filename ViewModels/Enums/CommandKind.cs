namespace JingleBox2.ViewModels.Enums;

/// <summary>Which command the command popup is setting.</summary>
public enum CommandKind
{
    /// <summary><c>Qxx</c>: start the note later in its line.</summary>
    Delay,

    /// <summary><c>Cxx</c>: silence the note partway through its line.</summary>
    Cut,

    /// <summary><c>Rxy</c>: play the note again within its line.</summary>
    Retrigger,

    /// <summary><c>Axy</c>: step the note through a chord.</summary>
    Arpeggio,

    /// <summary><c>Vxx</c>: the note's own volume.</summary>
    Volume,

    /// <summary><c>Pxx</c>: the note's own place left to right.</summary>
    Pan
}
