using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <summary>What every machine without per-process capture gets.</summary>
/// <remarks>
/// The recorder holds an <see cref="IProgramCapture"/> whatever the machine turns out to be, so
/// the absence of one is an object that politely says no rather than a null every caller has to
/// remember to check. On Linux this is what is used and nothing is lost: there a program is a
/// node in the graph and the routing already points the input at one.
/// </remarks>
public sealed class NoProgramCapture : IProgramCapture
{
    /// <inheritdoc/>
    /// <remarks>Always false. There is nothing here to be available.</remarks>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>Nothing, always.</remarks>
    public IReadOnlyList<AudioProgram> Programs() => Array.Empty<AudioProgram>();

    /// <inheritdoc/>
    public int SampleRate => 0;

    /// <inheritdoc/>
    public int Channels => 0;

    /// <inheritdoc/>
    public bool IsRunning => false;

    /// <inheritdoc/>
    /// <remarks>Refuses everything, since there is nothing to listen with.</remarks>
    public bool Start(int processId, Action<byte[]> onAudio) => false;

    /// <inheritdoc/>
    public void Stop() { }

    /// <inheritdoc/>
    /// <remarks>Holds nothing, so there is nothing to let go of.</remarks>
    public void Dispose() { }
}
