namespace JingleBox2.Audio.Enums;

/// <summary>Which way out of the machine an audio device is reached by.</summary>
public enum AudioOutputKind
{
    /// <summary>
    /// Whatever the operating system offers, which is what everything used before ASIO.
    /// </summary>
    /// <remarks>
    /// First, and nought, so a settings file written before ASIO existed names one of these and
    /// goes on meaning what it meant.
    /// </remarks>
    System,

    /// <summary>
    /// A driver written for the card itself, which is how Windows gets out of the way.
    /// </summary>
    /// <remarks>
    /// ASIO is Steinberg's, and it is Windows only in practice. It is not a list of endpoints
    /// like the system's: it is a list of drivers, each of which owns the whole card while it is
    /// open, so the numbers in it have nothing to do with the system's numbers and the two lists
    /// can only be put together by saying which is which.
    /// </remarks>
    Asio
}
