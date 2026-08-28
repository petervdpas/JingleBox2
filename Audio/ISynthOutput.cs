using System;

namespace JingleBox2.Audio;

/// <summary>
/// The tracker's way out to the speakers: one stream, pulled at by the sound card and filled by
/// the mixer.
/// </summary>
/// <remarks>
/// One stream for the whole song rather than a channel per note, because the voices are generated
/// here and summing them in managed code is cheaper than handing the sound library dozens of
/// channels to keep in step.
///
/// The rate is fixed for the life of the mixer. Voices, filters and plugins all work their
/// timings out from it, so it cannot move under them: it is asked for once, when the device is
/// opened, and everything is built for it after that.
/// </remarks>
public interface ISynthOutput : IDisposable
{
    /// <summary>What the engine is running at.</summary>
    int SampleRate { get; }

    /// <summary>The mixer, built the first time anything asks for it.</summary>
    /// <remarks>
    /// Late on purpose: until the audio device has been opened there is no way to know what rate
    /// to build it for.
    /// </remarks>
    Tracker.Synth.TrackMixer Mixer { get; }

    /// <summary>True once the mixer exists, so a meter can ask without building one.</summary>
    bool HasMixer { get; }

    /// <summary>Whether the stream is open.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// The loudest thing this stream is putting out, 0 to 1. The tracker's half of the main
    /// output meter; the pads are the other half and are their own channels.
    /// </summary>
    float Level { get; }

    /// <summary>How far ahead the mixer is asked to work, in milliseconds.</summary>
    int RenderAheadMilliseconds { get; }

    /// <summary>How many frames the cushion has failed to supply since the stream opened.</summary>
    long Underruns { get; }

    /// <summary>
    /// Asks for a rate, or for the device's own with <see cref="SynthOutput.FollowDevice"/>.
    /// </summary>
    /// <remarks>
    /// Only heard before the mixer is built, which is why it comes from the settings at startup.
    /// </remarks>
    /// <param name="rate">Frames a second, or nought to follow the device.</param>
    void UseSampleRate(int rate);

    /// <summary>How far ahead to mix, in milliseconds. Nought mixes in step with the sound card.</summary>
    /// <remarks>
    /// The reason this exists is plugins. A plugin runs in a process of its own and every block it
    /// plays is a message out and a message back, made from the thread that has ten milliseconds
    /// to fill a buffer. That thread cannot be asked to wait on somebody else's scheduler, and
    /// when it does, what comes out is a hole. Mixing ahead moves that work onto a thread of its
    /// own, which leaves finished audio in a queue for the sound card to take. What it costs is
    /// the size of the queue: the sound you hear was mixed that long ago.
    ///
    /// Read when the stream is opened, so a change takes effect the next time the audio starts.
    /// </remarks>
    /// <param name="milliseconds">How much cushion, clamped to something sensible.</param>
    void UseRenderAhead(int milliseconds);

    /// <summary>
    /// Opens the stream on first use, and opens it again if it has gone. Safe to call before
    /// every note.
    /// </summary>
    /// <remarks>
    /// Changing the output device closes the sound library and opens it again, which takes this
    /// stream with it without telling anybody. So the handle is not taken as proof: the stream has
    /// to still be running, or it is made again.
    /// </remarks>
    /// <param name="audio">The engine that owns the device, opened first if it is not.</param>
    void EnsureStarted(IAudioEngine audio);

    /// <summary>Silences the voices. The stream stays open, ready for the next note.</summary>
    void Silence();
}
