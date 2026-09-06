using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// A finished take run through an effect chain, offline.
/// </summary>
/// <remarks>
/// RECORD keeps a chain of its own, and what it does to a take is done here rather than on the
/// way in. **Nothing is heard while a take is being made**, so there is no reason whatever to
/// put an effect on the thread the capture callback runs on: a plugin in its own process costs a
/// crossing per block, and paying that where a late block is a hole in somebody's only copy of a
/// performance would be the worst trade in this application. Offline the chain may take as long
/// as it likes, and the answer is the same, because a chain is a stream: handed a take in blocks
/// it makes exactly what it would have made in real time.
///
/// What comes back is always <see cref="Channels"/> wide, whatever went in. An effect places
/// things in the stereo field, so a mono take run through one has two sides afterwards and
/// saying otherwise would mean throwing half of the result away.
///
/// **The processed take is exactly as long as the take it came from.** A delay still ringing
/// when the last frame goes past is cut off with it, which is a decision rather than an
/// oversight: the two takes then lie on top of each other frame for frame, which is what makes
/// keeping both worth anything.
/// </remarks>
public interface ITakeEffects
{
    /// <summary>How many channels what comes back has, whatever the take had.</summary>
    int Channels { get; }

    /// <summary>
    /// Runs a whole take through a chain and gives back what came out.
    /// </summary>
    /// <remarks>
    /// Both ends are 16 bit samples, little endian, interleaved, which is what the capture hands
    /// over and what a WAV here is written from. A take of more than two channels is read as its
    /// first two, since that is what a chain works on.
    ///
    /// The scaling is the same number in both directions, so a chain with nothing on it hands
    /// back the bytes it was given rather than nearly them. Anything that is not a number is
    /// written out as silence: this ends up in a file rather than at the converters, and a file
    /// full of NaN is one that plays as full scale noise the first time anybody opens it.
    /// </remarks>
    /// <param name="pcm">The take, as it was captured.</param>
    /// <param name="channels">How wide that take is.</param>
    /// <param name="effect">What to run it through.</param>
    /// <param name="maxFrames">The longest block the chain was built for.</param>
    /// <returns>The processed take, or nothing at all where there was nothing to work on.</returns>
    byte[] Through(byte[] pcm, int channels, IAudioInsert effect, int maxFrames);

    /// <summary>
    /// Hands the chain silence, so that whatever the take before it left inside has died away.
    /// </summary>
    /// <remarks>
    /// A chain is used again for the next take and holds its own state between the two: a delay
    /// line still full of the end of one take would repeat it over the beginning of the next,
    /// which reads as the recorder having captured something that was never there. Silence in is
    /// what a hardware effect gets between two takes, and it costs a moment of arithmetic that
    /// nobody is waiting on.
    ///
    /// It cannot help a chain that never decays, a delay at full feedback being the obvious one.
    /// There is no way to ask an insert to forget, and adding one is a change to a contract this
    /// does not need.
    /// </remarks>
    /// <param name="effect">The chain to quieten.</param>
    /// <param name="frames">How much silence to give it.</param>
    /// <param name="maxFrames">The longest block the chain was built for.</param>
    void Settle(IAudioInsert effect, int frames, int maxFrames);
}
