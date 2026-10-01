using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
/// <remarks>
/// The runtime is told apart by its own file name, which is <c>dotnet</c> on every platform it
/// ships for. The first entry of the command line is the program or the library and the rest is
/// what followed it: the program runs again with the rest, the runtime with all of it.
/// </remarks>
public sealed class Relaunch : IRelaunch
{
    /// <inheritdoc/>
    public ProcessStartInfo Again(string processPath, IReadOnlyList<string> commandLine)
    {
        var start = new ProcessStartInfo(processPath) { UseShellExecute = false };

        bool runtime = string.Equals(Path.GetFileNameWithoutExtension(processPath.Replace('\\', '/').Split('/')[^1]),
                                     "dotnet", StringComparison.OrdinalIgnoreCase);

        for (int at = runtime ? 0 : 1; at < commandLine.Count; at++) start.ArgumentList.Add(commandLine[at]);

        return start;
    }
}
