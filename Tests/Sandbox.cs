using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace JingleBox2.Tests;

/// <summary>
/// Points the application folder somewhere disposable before any test runs.
/// </summary>
/// <remarks>
/// Several of the things worth testing read and write the application folder: profiles and
/// codecs are copied into it on first run, and a history of the pads is a history of what is
/// stored there. A test that used the real one would read whatever happens to be on the machine
/// it runs on, and worse, would write to somebody's settings.
///
/// Once for the whole assembly, and the tests do not run in parallel, because an environment
/// variable belongs to the process rather than to a test and two classes racing to set it would
/// be a fault nobody could reproduce.
/// </remarks>
internal static class Sandbox
{
    /// <summary>
    /// Points both of the places the application folder can be worked out from at a fresh
    /// temporary folder, named after the process so two runs on one machine cannot meet.
    /// </summary>
    /// <remarks>
    /// A module initialiser rather than a fixture, because it has to have happened before the
    /// first line of the first test: the folder is read by static state that is built on first
    /// use, and a fixture runs too late to move it. Both variables are set, since the folder is
    /// worked out from XDG_CONFIG_HOME on Linux and APPDATA on Windows and the suite runs on
    /// both.
    /// </remarks>
    [ModuleInitializer]
    internal static void Somewhere()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jinglebox2-tests-" + Environment.ProcessId);

        Directory.CreateDirectory(folder);

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", folder);
        Environment.SetEnvironmentVariable("APPDATA", folder);
    }
}
