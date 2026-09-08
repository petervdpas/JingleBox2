namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What the recorder's bus is summing, kept so it can be written down as the take.
/// </summary>
/// <remarks>
/// **An input point is a merge, and this is the end of the merge that reaches the file.** Every
/// cable pointed at RECORD lands on one bus: the capture the sound card handed over, and any
/// source of this application somebody has patched across. What is heard is that bus, and what is
/// recorded has to be the same bus, or the picture is telling one story and the file another.
/// It was: two cables landed on the one input point, both sounded, and only the capture was ever
/// written, because the take was built out of the bytes the capture handed over and nothing else
/// could reach it.
///
/// Read off the bus rather than off its sources, so it needs to know nothing about what is on it
/// and a source patched across later is carried without anybody saying so.
///
/// **Before the bus's own level rather than after**, which is what makes Hear it and Record two
/// separate questions. A hook on a channel runs as that channel produces its audio and the bus
/// its output goes to applies the level afterwards, so a take is complete at full scale while the
/// desk is hearing nothing at all. That is what a record bus on a desk has always been.
/// </remarks>
public interface ITakeTap
{
    /// <summary>Which bus to keep, which is the recorder's own.</summary>
    /// <remarks>
    /// Told rather than found, and told again whenever the bus is made again: an output device
    /// that changes takes every bus with it, and a hook on a handle that has gone is a hook on
    /// whatever is given that number next.
    /// </remarks>
    /// <param name="bus">The recorder's bus.</param>
    void Follow(IOutputBus bus);

    /// <summary>Begins keeping what the bus sums.</summary>
    /// <remarks>
    /// What was kept before is let go here rather than at the stop, the same rule the capture's
    /// own buffer keeps: a take that is thrown away is thrown away when the next one starts, so
    /// nothing is lost by a stop that never came.
    /// </remarks>
    void Start();

    /// <summary>Stops keeping, and hands back what was kept.</summary>
    /// <returns>The take off the bus, or nothing where none was being made.</returns>
    byte[] Stop();

    /// <summary>The take from the last <see cref="Stop"/>.</summary>
    byte[] Take { get; }

    /// <summary>Frames a second what was kept is at, which is the bus's own rate.</summary>
    int Rate { get; }

    /// <summary>How wide it is, which is the bus's own width.</summary>
    int Channels { get; }

    /// <summary>
    /// Whether the bus was carrying anything besides the capture.
    /// </summary>
    /// <remarks>
    /// **This is what decides whether the take comes off the bus at all.** With nothing patched
    /// in, the bus is carrying the capture and nothing else, and the capture's own bytes are the
    /// better copy of it: they are at the rate it arrived at, they are never resampled, and they
    /// cannot be missed however busy the machine is. So a take made the way every take has been
    /// made until now is written exactly as it always was, and the bus is only read where it is
    /// carrying something the capture cannot know about.
    /// </remarks>
    bool Mixed { get; }

    /// <summary>Lets the bus go.</summary>
    void Close();
}
