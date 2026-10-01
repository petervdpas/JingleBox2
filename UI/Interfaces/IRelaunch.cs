using System.Collections.Generic;
using System.Diagnostics;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// How to start the application again the way it was started, for a restart it does itself.
/// </summary>
/// <remarks>
/// A setting that only takes effect at the next start, the sample rate above all, offers to
/// restart there and then. The new run is started only once this one has finished closing, so two
/// never run at once, and an installed copy, <c>dotnet run</c> and <c>dotnet JingleBox2.dll</c> each
/// come back the way they went.
/// </remarks>
public interface IRelaunch
{
    /// <summary>What to start to run the application again.</summary>
    /// <param name="processPath">The file this process runs, as <c>Environment.ProcessPath</c> gives it.</param>
    /// <param name="commandLine">The command line, as <c>Environment.GetCommandLineArgs</c> gives it.</param>
    ProcessStartInfo Again(string processPath, IReadOnlyList<string> commandLine);
}
