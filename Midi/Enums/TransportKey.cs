namespace JingleBox2.Midi.Enums;

/// <summary>Which of the transport's four keys a hardware button is pointed at.</summary>
/// <remarks>
/// The four that are drawn, which is one more than <c>ITransportKeys</c> carries. That interface
/// is what a controller's own transport buttons ask for and folds pause into stop, because a
/// device sending MMC pause is asking to stop and there is nowhere here to pause to. This is the
/// other direction: somebody pointing a button at the pause on the screen means that button, and
/// the button is there.
/// </remarks>
public enum TransportKey
{
    /// <summary>Start, or carry on.</summary>
    Play,

    /// <summary>Hold where it is.</summary>
    Pause,

    /// <summary>Stop, and go back to the beginning.</summary>
    Stop,

    /// <summary>Arm, or start recording.</summary>
    Record,

    /// <summary>
    /// Cycle: turns looping on or off.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the key it was given. It is
    /// here rather than only in the file's vocabulary because a cycle key is a transport key
    /// everywhere else: Mackie Control puts it in the transport row, so does every controller
    /// anybody makes, and so does the bar at the top of this window once the Loop switch is
    /// counted.
    /// </remarks>
    Loop
}
