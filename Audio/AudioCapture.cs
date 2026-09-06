using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class AudioCapture : IAudioCapture
{
    /// <inheritdoc/>
    /// <remarks>
    /// **Windows 10 build 20348 is the floor**, which is where per-process loopback arrived: an
    /// older Windows has the call and refuses the activation, so asking the version is the only
    /// way to tell that apart from a program that simply cannot be captured.
    ///
    /// Linux gets the one that says no, and loses nothing by it: there a program is a node in
    /// the graph and the routing already points the input straight at it.
    /// </remarks>
    public IProgramCapture Programs() =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)
            ? new ProcessLoopback()
            : new NoProgramCapture();
}
