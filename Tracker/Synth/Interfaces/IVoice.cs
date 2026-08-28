using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// One sounding note, whatever it is made of. The mixer treats a recording and a generated
/// wave the same way: both take a level and a placement, both answer to a note off, and both
/// add themselves into the buffer.
/// </summary>
/// <remarks>
/// A voice is made on whichever thread started the note and is then rendered on the audio
/// thread. <see cref="Render"/> is therefore the one method that runs there, and it may not
/// allocate, take a lock or wait on anything: everything it needs is worked out when the note
/// starts. The mixer holds a voice's list behind a lock and renders off a snapshot, so at most
/// one thread is ever inside a voice at a time.
///
/// The level and the placement are settable while the note holds, because the tracker's volume
/// and pan columns can move under a note that is already sounding. Reading one back is a fresh
/// read of a field, which is exactly as true as it is at that instant and no truer.
/// </remarks>
public interface IVoice
{
    /// <summary>The track this note belongs to, or <see cref="SynthVoice.NoTrack"/> for an audition.</summary>
    int Track { get; }

    /// <summary>
    /// Which instrument played this by hand, or empty for a note from a pattern.
    /// </summary>
    /// <remarks>
    /// An audition belongs to no track, so a track number cannot tell two panels apart. This
    /// can, which is what an instrument set to one voice needs: cut the note I was sounding,
    /// not the one another machine is.
    /// </remarks>
    string Audition { get; }

    /// <summary>The note as the cell or the key gave it, which is what a choke and a note off match on.</summary>
    Note Note { get; }

    /// <summary>Level from the cell and the instrument, changeable while the note holds.</summary>
    float Gain { get; set; }

    /// <summary>
    /// Where it sits, -1 hard left to 1 hard right.
    /// </summary>
    /// <remarks>
    /// A balance rather than an equal-power pan: the centre stays at full level on both sides,
    /// which is what BASS does for the pads, so a voice and a pad sit together in the mix
    /// rather than one of them being three decibels down in the middle.
    /// </remarks>
    float Pan { get; set; }

    /// <summary>How loud this voice is right now, for metering. Zero once it has finished.</summary>
    float Level { get; }

    /// <summary>Silent and done, so the mixer can drop it after the block it finished in.</summary>
    bool IsFinished { get; }

    /// <summary>
    /// True for a sound with an end of its own, which a hand cannot cut short.
    /// </summary>
    /// <remarks>
    /// A recording that does not loop is one: it is a hit, and a hit played by hand sounds right
    /// through whether the mouse came up after two seconds or after two milliseconds. A take cut
    /// off part way is not the sound the instrument makes, and a click is a few milliseconds
    /// long, so following the key would make every clicked note a tick.
    ///
    /// Only against a hand. A pattern's OFF cuts anything, which is the whole of what an OFF is
    /// for, and a track is one voice regardless.
    ///
    /// False for everything else, because everything else would ring for ever without a key to
    /// let go of: a generated wave has no end, and neither has a looping window.
    /// </remarks>
    bool OneShot => false;

    /// <summary>Releases on its own after this long, for auditioning with no key to let go of.</summary>
    void HoldFor(double seconds);

    /// <summary>
    /// Lets go of the note, which starts its release rather than stopping it.
    /// </summary>
    /// <remarks>
    /// What a pattern's OFF does and what a key coming up does. A sound with a long tail keeps
    /// its tail, which is the whole difference between this and <see cref="Cut"/>.
    /// </remarks>
    void NoteOff();

    /// <summary>A short fade rather than a full release, for a retrigger on the same track.</summary>
    void Cut();

    /// <summary>
    /// Silent at once, for a transport stop rather than a note ending.
    /// </summary>
    /// <remarks>
    /// The only one of the three that can click, and the only one that is allowed to: pressing
    /// stop is a request for silence now, not for every tail in the song to play out first.
    /// </remarks>
    void Kill();

    /// <summary>
    /// Adds this voice into an interleaved stereo buffer, on top of what is there.
    /// </summary>
    /// <remarks>
    /// Runs on the audio thread. Additive rather than overwriting, because the mixer sums every
    /// voice on a track into that track's one bus, and a plugin may already have filled it.
    /// </remarks>
    void Render(float[] buffer, int frames);
}
