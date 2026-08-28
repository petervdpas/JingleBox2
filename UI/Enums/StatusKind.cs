namespace JingleBox2.UI.Enums;

/// <summary>How a message wants to be read.</summary>
public enum StatusKind
{
    /// <summary>Where you are and what is selected. The resting state of the bar.</summary>
    Context,

    /// <summary>Something happened. Most messages.</summary>
    Plain,

    /// <summary>Something worked, and saying so is worth a moment of green.</summary>
    Done,

    /// <summary>Something is not as expected, but nothing has broken.</summary>
    Warning,

    /// <summary>Something failed. These stay until the next thing, and go in the log as well.</summary>
    Fault
}
