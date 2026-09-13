namespace JingleBox2.SoundDevices.SoundEffects.Interfaces;

/// <summary>
/// The volume an effect hands its audio back at: one knob, in decibels, the last thing a block
/// goes through.
/// </summary>
/// <remarks>
/// Every effect of ours has one and it is the same knob on each, so it is written once. An
/// effect changes how loud a track is whether it means to or not: a phaser at half mix cuts
/// whole bands out, a ring modulator takes the fundamental away, and a delay adds its repeats on
/// top of what was there. Without a level of its own the only way to put a track back where it
/// was is its fader, and the fader is then saying something about the effect rather than about
/// the part.
///
/// The word is <see cref="Key"/>, which is what a manifest names, a preset stores and a link
/// points at. Nought decibels is where it starts and is exactly nothing: a block at unity with
/// the knob settled is not touched, so an effect on a chain saved before the knob existed sounds
/// the same sample for sample.
///
/// **A move is a ramp across the block, not a step at its start.** A controller sends a knob in
/// a hundred and twenty eight steps and each lands between two blocks, so a gain that jumped
/// would put a corner in the wave at every one of them, which is heard as a zipper. Ramped over
/// one block the corner is gone and the knob still arrives within a block of the hand.
///
/// Set from the drawing thread or the MIDI thread and applied on the audio thread, so what is
/// stored is a single word, and nothing here allocates, takes a lock or blocks.
/// </remarks>
public interface IEffectLevel
{
    /// <summary>The word every effect's manifest names this knob by.</summary>
    const string Key = "level";

    /// <summary>The quietest it goes, in decibels.</summary>
    const double LeastDb = -24;

    /// <summary>The loudest, in decibels.</summary>
    const double MostDb = 12;

    /// <summary>Where the knob stands, in decibels.</summary>
    double Db { get; }

    /// <summary>Puts the knob there, held to its ends; anything that is not a number is refused.</summary>
    /// <param name="db">Where to put it, in decibels.</param>
    void Set(double db);

    /// <summary>Brings the first frames of that block to the level the knob stands at.</summary>
    /// <remarks>
    /// The first block after the level was set before any audio is taken at the new level
    /// outright, since there is nothing to ramp from: a song opening at minus six starts at minus
    /// six rather than fading down to it.
    /// </remarks>
    /// <param name="buffer">Interleaved stereo, worked on in place.</param>
    /// <param name="frames">How many frames of it are real, already held to the buffer.</param>
    void Apply(float[] buffer, int frames);
}
