using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// One variable, read each time rather than once, so it can be answered differently in a test
/// without a process boundary. Reading an environment variable is a dictionary lookup and this is
/// asked when an output is opened, which happens when somebody picks a device.
/// </remarks>
public sealed class BusSwitch : IBusSwitch
{
    /// <summary>What turns it on, which is the same shape as the other two switches here.</summary>
    public const string Variable = "JB_BUS";

    /// <inheritdoc/>
    public bool Wanted => Environment.GetEnvironmentVariable(Variable) == "1";

    /// <summary>
    /// Says what the settings hold, for everything after this.
    /// </summary>
    /// <remarks>
    /// Called once at startup before any output is opened, and again whenever the tick moves.
    /// Written into the environment rather than kept in a field for the reason
    /// <c>RealtimeThread.Wants</c> is: the answer has to be readable by things that hold no
    /// settings, and it keeps one place where the question is asked.
    ///
    /// Which means a run started with <c>JB_BUS=1</c> on the command line is overruled by the
    /// stored tick the moment this is called. That is the right way round now the tick exists:
    /// the variable was how this was reached while there was nothing to tick, and a setting
    /// somebody chose should not be quietly beaten by a shell.
    /// </remarks>
    /// <param name="wanted">Whether the summing is asked for.</param>
    public static void Wants(bool wanted) =>
        Environment.SetEnvironmentVariable(Variable, wanted ? "1" : "0");
}
