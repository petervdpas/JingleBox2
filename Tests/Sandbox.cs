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
    [ModuleInitializer]
    internal static void Somewhere()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jinglebox2-tests-" + Environment.ProcessId);

        Directory.CreateDirectory(folder);

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", folder);
        Environment.SetEnvironmentVariable("APPDATA", folder);
    }
}
