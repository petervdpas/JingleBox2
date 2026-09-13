using System.Collections.Generic;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Listens to a recording of drums and says what was hit, where, and which of those make a kit.
/// </summary>
/// <remarks>
/// **Rules, not a model.** What a drum is can be told the way anybody listening tells it: a kick
/// is weight and is gone quickly, a hat is all top, a snare is a body with a rattle over it, a
/// cymbal is top that will not stop. So a hit is judged on three things that can be measured: how
/// its energy is shared between the bottom, the middle and the top, how long it takes to die
/// away, and how noisy it is. Nothing is trained and nothing is guessed at: the same recording is
/// judged the same way every time, and a judgement that is wrong can be traced to the number it
/// was made on.
///
/// **A hit is found by how fast something rises, band by band.** Loudness alone misses a hat
/// played over the tail of a kick, since the kick is still the loudest thing ringing; but the top
/// of the sound goes from nothing to something in a few milliseconds, and that rise is the hat.
/// So each of the three bands is followed on its own, a hit is wherever any of them jumps well
/// above where it just was, and hits closer together than a drummer could play are one.
///
/// **A kit is one of each sound, not every hit.** A beat plays the same kick again and again, so
/// hits are grouped with the others that sound alike and one is kept from each group: the one
/// that rang longest before the next hit landed on it, since that is the one with least of
/// anything else in it. The groups are laid out in the order a kit is, kick first, and the most
/// used group of a kind before a less used one.
/// </remarks>
public interface IDrumListener
{
    /// <summary>Every hit in the recording, in the order they are played.</summary>
    /// <param name="sample">The recording, decoded.</param>
    IReadOnlyList<DrumHit> Listen(SampleData? sample);

    /// <summary>One hit of each sound among those, in the order a kit is laid out, at most that many.</summary>
    /// <param name="hits">What <see cref="Listen"/> found.</param>
    /// <param name="pads">How many pads there are to fill.</param>
    IReadOnlyList<DrumHit> Kit(IReadOnlyList<DrumHit>? hits, int pads);

    /// <summary>What a pad holding that hit is called: the sound, and a number where the kit has two of it.</summary>
    /// <param name="kit">What <see cref="Kit"/> chose, in pad order.</param>
    /// <param name="at">Which of them.</param>
    string NameOf(IReadOnlyList<DrumHit> kit, int at);

    /// <summary>
    /// What each of those windows of the recording is called, by the loudest hit that begins in it.
    /// </summary>
    /// <remarks>
    /// For a recording cut into pieces in the order it plays rather than sorted into a kit: every
    /// piece is named for the drum it starts with, numbered where two pieces start with the same
    /// one. A window no hit begins in is named for the hit still ringing into it, and one with
    /// nothing at all is percussion.
    /// </remarks>
    /// <param name="hits">What <see cref="Listen"/> found in the recording.</param>
    /// <param name="windows">Each piece, as fractions of the recording.</param>
    IReadOnlyList<string> Names(IReadOnlyList<DrumHit>? hits, IReadOnlyList<(double Start, double End)>? windows);
}
