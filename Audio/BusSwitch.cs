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
}
