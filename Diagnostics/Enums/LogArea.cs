using System;

namespace JingleBox2.Diagnostics.Enums;

/// <summary>What a line in the log is about, so a log can be read without reading all of it.</summary>
/// <remarks>
/// Flags rather than a list, because the useful question is nearly always "everything about
/// plugins and nothing else" and that has to be one comparison in a place where lines are
/// written thousands of times a second.
/// </remarks>
[Flags]
public enum LogArea
{
    /// <summary>Nothing is written, which is what the log is unless somebody asked.</summary>
    None = 0,

    /// <summary>Starting up, settings, files.</summary>
    App = 1 << 0,

    /// <summary>The audio engine, devices, pads.</summary>
    Audio = 1 << 1,

    /// <summary>Plugins: loading, windows, parameters, the processes they run in.</summary>
    Plugins = 1 << 2,

    /// <summary>The tracker: patterns, the song, what marks it as unsaved.</summary>
    Tracker = 1 << 3,

    /// <summary>MIDI in and where it is routed.</summary>
    Midi = 1 << 4,

    /// <summary>
    /// The machines: what is installed, what is read off a machine's own folder, and what a
    /// machine's face is built from.
    /// </summary>
    /// <remarks>
    /// Its own area rather than part of the app's, because it is a whole half of this program
    /// and it says almost nothing while nothing is wrong. What it is for is the day a machine
    /// draws an empty panel or comes back from a zip missing a picture, and on that day the
    /// last thing anybody wants is to read it out of everything the application did at startup.
    /// </remarks>
    Machines = 1 << 5,

    /// <summary>Every area there is, which is what <c>JB_LOG=1</c> and the settings tick ask for.</summary>
    Everything = App | Audio | Plugins | Tracker | Midi | Machines
}
