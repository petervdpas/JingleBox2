using System;
using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>Listening to one program on this machine, rather than to a device or an output.</summary>
/// <remarks>
/// **The third capture path, and the one that makes Windows behave the way Linux does.** A
/// PipeWire machine can point the recorder at a browser because every stream on it is something
/// that can be patched; Windows has no such graph, and this is what stands in for that one
/// question. Underneath it is process loopback, which is Windows 10 build 20348 and later.
///
/// It copies rather than moves, and that is a fact about the system rather than about this: the
/// program goes on playing out of whatever it was playing out of, so it is heard twice unless it
/// is also pointed somewhere nobody is listening. That is a separate act.
///
/// Everything above still meets sixteen bit interleaved audio, so the gain, the meter, the clip
/// lamp and the writer are unchanged and only where the audio came from is different.
/// </remarks>
public interface IProgramCapture : IDisposable
{
    /// <summary>Whether this machine can do it at all.</summary>
    /// <remarks>
    /// False is an ordinary answer. Off Windows there is no such call; on an older Windows the
    /// activation is refused, and the recorder then keeps to devices and outputs.
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>Every program with audio to give right now.</summary>
    IReadOnlyList<AudioProgram> Programs();

    /// <summary>What the capture is running at, once it has started.</summary>
    int SampleRate { get; }

    /// <summary>How many channels the audio arrives in.</summary>
    int Channels { get; }

    /// <summary>Whether audio is arriving.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts listening to one program and the programs it started.
    /// </summary>
    /// <remarks>
    /// The tree rather than the one process, because that is what a program is: a browser is a
    /// dozen processes and the one making the sound is not the one anybody picked.
    ///
    /// A program that is not playing gives silence rather than a refusal, which is better than
    /// the Linux side manages: there a source that is not making a sound is not in the graph to
    /// be chosen at all.
    /// </remarks>
    /// <param name="processId">Which program, from <see cref="Programs"/>.</param>
    /// <param name="onAudio">Called from the capture's own thread with each block.</param>
    /// <returns>False where this is not available or the capture will not start.</returns>
    bool Start(int processId, Action<byte[]> onAudio);

    /// <summary>Stops listening, and does nothing when nothing is being listened to.</summary>
    void Stop();
}
