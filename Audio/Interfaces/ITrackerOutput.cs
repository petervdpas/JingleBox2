using System;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The tracker's way out to the speakers: one stream, pulled at by the sound card and filled by
/// the mixer.
/// </summary>
/// <remarks>
/// It was called SynthOutput, which was true when the tracker was synth voices and nothing else
/// and said the wrong thing about the one stream the whole song leaves through. What crosses it
/// is every voice there is: synth patches, recordings, kits, and plugins in processes of their
/// own, summed by <see cref="Tracker.Synth.Interfaces.ITrackMixer"/> and through the master.
/// The mixer had the same name and was renamed for the same reason.
///
/// The pads are the one thing that does not come through here: each of those is a stream of its
/// own on the sound library, which is what lets one be started and stopped without the song
/// noticing. Recordings written to disc do not either; that is RECORD's own path.
///
/// One stream for the whole song rather than a channel per note, because the voices are generated
/// here and summing them in managed code is cheaper than handing the sound library dozens of
/// channels to keep in step.
///
/// The rate is fixed for the life of the mixer. Voices, filters and plugins all work their
/// timings out from it, so it cannot move under them: it is asked for once, when the device is
/// opened, and everything is built for it after that.
///
/// **The thread contract, which is written down in full in <c>docs/threads.md</c>.**
///
/// This is where the mixer's two callers come from, and it is the only place in the application
/// that has any. The sound card's own thread calls the fill; the mixing-ahead thread, when there
/// is one, renders in advance into a ring and the fill only takes from it. Which of the two ways
/// is running is one volatile number, written by the drawing thread while starting or stopping.
/// The ring itself is one lock and nothing else touches it.
///
/// **Stopping the ahead thread is not a guarantee that it has stopped.** It is given two tenths
/// of a second and then left to finish on its own, because a plugin taking its time inside a
/// block must not hang the application, and that is exactly how both threads come to be inside
/// the mixer at once. The mixer's own guard is what makes it safe rather than anything here, and
/// a thread that would not stop in time is written to the log, since it means a plugin took
/// longer than a fifth of a second over one block.
///
/// Everything else here is the drawing thread: opening a device, choosing how far ahead to run,
/// and disposing.
/// </remarks>
public interface ITrackerOutput : IDisposable
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
    /// Asks for a rate, or for the device's own with <see cref="TrackerOutput.FollowDevice"/>.
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

    /// <summary>
    /// How far ahead the stream is buffered, in milliseconds. Say it before it is opened.
    /// </summary>
    /// <remarks>
    /// The latency: what the card is playing is what was mixed this long ago, so it is how long
    /// a key waits before it sounds. Not the same question as the render-ahead cushion, which
    /// moves the mixing off the sound card's thread and costs its own milliseconds on top of
    /// these; this is how much finished audio the card keeps in hand.
    ///
    /// Held between what BASS can usefully keep and what stops being worth having. The rate at
    /// which the buffer is topped up follows from it rather than being asked for separately: a
    /// period that cannot keep up with the buffer is a dropout with no other explanation.
    /// </remarks>
    /// <param name="milliseconds">How much to hold. Clamped rather than refused.</param>
    void UseBuffer(int milliseconds);

    /// <summary>Silences the voices. The stream stays open, ready for the next note.</summary>
    void Silence();
}
