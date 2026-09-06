using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Audio;

/// <summary>
/// Whether a block's crossings to plugin processes are begun together or one after another.
/// </summary>
/// <remarks>
/// Static, for the reason the other doors here are: an application has one audio path and this is
/// one setting, so handing it about would be handing the same object about under another name.
/// Nothing in it decides anything, which is the rule every door keeps: what a run in parts is and
/// how one is driven is <see cref="Plugins.Interfaces.IOverlappable"/>, which can be put a
/// question to without a plugin, a process or a settings file.
///
/// **Not an environment variable**, unlike the real-time one. That is asked when an output is
/// opened, which is when somebody picks a device; this is asked once a block on the thread the
/// sound card is waiting on.
///
/// Off unless somebody says otherwise. The audio is identical either way, since the change is
/// when a plugin is asked rather than what it is asked, but it is the audio path in a program
/// where a plugin lives in another process and can die between the asking and the answer, so it
/// ships off and is turned on by somebody who has looked at the two log lines.
/// </remarks>
public static class OverlapSwitch
{
    /// <summary>Whether the crossings are begun together.</summary>
    public static bool Wanted { get; private set; }

    /// <summary>Says what the settings hold, for everything after this.</summary>
    /// <remarks>
    /// It says so in the log, for the reason <see cref="TangentSwitch"/> does: reading the two
    /// arrangements against each other means running the same song twice, and the switch moves
    /// without stopping the transport, so without a line the two halves land in one file with
    /// nothing between them.
    /// </remarks>
    /// <param name="wanted">Whether overlapping is asked for.</param>
    public static void Wants(bool wanted)
    {
        Wanted = wanted;

        Log.Write(LogArea.Audio, () => "plugin blocks: " + (wanted ? "begun together" : "one track at a time"));
    }
}
