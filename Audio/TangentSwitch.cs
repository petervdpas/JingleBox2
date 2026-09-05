using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Audio;

/// <summary>
/// Which of the two curves the audio path is bending with, and the switch that changes it.
/// </summary>
/// <remarks>
/// Static, and one of the few here that is: an application has one audio path, the answer is one
/// setting, and handing it about would be handing the same object about under another name.
/// Nothing in it decides anything, which is the rule every door here keeps: what the two curves
/// are and how far apart they are is <see cref="ITangent"/> and its two implementations, either
/// of which can be put a question to without a process, a sound card or a settings file.
///
/// **Not an environment variable, which is how the other two engine switches are read.**
/// <see cref="BusSwitch"/> and the real-time one are asked when an output is opened, which is
/// when somebody picks a device; this is asked for every sample of every sounding voice, and a
/// dictionary lookup there would cost more than the curve it is choosing between.
///
/// Both curves are made when this is first touched, so the table is drawn at startup rather than
/// on the audio thread. Throwing the switch is one reference write and takes effect within the
/// block being mixed, which is what makes it something somebody can sit and listen to both ways.
/// </remarks>
public static class TangentSwitch
{
    /// <summary>The system's own, which is what off means.</summary>
    private static readonly ITangent Exact = new Tangent();

    /// <summary>The drawn one, which is what on means.</summary>
    private static readonly ITangent Drawn = new TableTangent();

    /// <summary>Which curve everything on the audio path is using now.</summary>
    public static ITangent Now { get; private set; } = Exact;

    /// <summary>Whether the drawn curve is the one running.</summary>
    public static bool Fast => ReferenceEquals(Now, Drawn);

    /// <summary>
    /// Says what the settings hold, for everything after this.
    /// </summary>
    /// <remarks>
    /// Called once at startup and again whenever the tick moves. Off unless somebody says
    /// otherwise, so a settings file that has never heard of this sounds exactly as it did.
    ///
    /// **It says so in the log, and that is the whole reason the line exists.** Comparing the two
    /// curves means running the same music twice and reading the render cost either side, and the
    /// switch moves without stopping the transport, so without a line the two halves of that
    /// experiment are in one file with nothing between them. Written here rather than at the tick,
    /// so the startup call marks it too and there is one place saying it.
    /// </remarks>
    /// <param name="fast">Whether the drawn curve is asked for.</param>
    public static void Wants(bool fast)
    {
        Now = fast ? Drawn : Exact;

        Log.Write(LogArea.Audio, () => "drive curve: " + (fast ? "drawn from the table" : "the system's own"));
    }
}
