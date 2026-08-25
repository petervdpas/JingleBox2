namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One sounding note, whatever it is made of. The mixer treats a recording and a generated
/// wave the same way: both take a level and a placement, both answer to a note off, and both
/// add themselves into the buffer.
/// </summary>
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

    Note Note { get; }

    /// <summary>Level from the cell and the instrument, changeable while the note holds.</summary>
    float Gain { get; set; }

    float Pan { get; set; }

    /// <summary>How loud this voice is right now, for metering. Zero once it has finished.</summary>
    float Level { get; }

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

    void NoteOff();

    /// <summary>A short fade rather than a full release, for a retrigger on the same track.</summary>
    void Cut();

    void Kill();

    /// <summary>Adds this voice into an interleaved stereo buffer, on top of what is there.</summary>
    void Render(float[] buffer, int frames);
}
