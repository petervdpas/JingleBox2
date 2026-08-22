using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Tracker;

/// <summary>
/// One of the things that make sound. A machine is part of the application and there is a fixed
/// set of them; what you make is an instrument on one.
/// </summary>
/// <remarks>
/// The distinction is the one a rack makes. You do not build a new machine, you take one and
/// program it, and the result is an instrument with a name of its own: "Kick" is an OddSkilla
/// set a certain way, not a kind of thing in itself.
///
/// <see cref="TrackerInstrumentKind"/> is the field that says which machine an instrument is on,
/// and its numbers are in every song and instrument file ever saved, so they do not move. This
/// is the readable side of the same fact: what the machine is called, what it is for, and what
/// it needs from the panel that shows it.
/// </remarks>
public sealed record Machine(
    TrackerInstrumentKind Kind,
    string Name,
    string Summary,
    bool IsOurs)
{
    /// <summary>
    /// The oscillator machine: a wave, an envelope, a filter, and the modulation to move them.
    /// </summary>
    /// <remarks>
    /// Everything it plays it generates, so it needs no file and travels inside a song as a
    /// handful of numbers. Its range is wider than "synth" suggests: a sine with a fast pitch
    /// envelope is a kick, and noise with a short decay is a hihat, which is why the sounds a
    /// fresh library is stocked with are all built on this one.
    /// </remarks>
    public static readonly Machine OddSkilla = new(
        TrackerInstrumentKind.Synth,
        "OddSkilla",
        "Oscillator synth. Wave, envelope, filter and modulation, generated as it plays.",
        true);

    /// <summary>
    /// A recording played back at a pitch. The machine that will become Zampler.
    /// </summary>
    /// <remarks>
    /// Not yet a machine of its own: it plays one recording, resampled, with the same envelope
    /// and filter OddSkilla uses. What would make it Zampler is holding a set of recordings
    /// mapped across the keyboard rather than a single one.
    /// </remarks>
    public static readonly Machine Sampler = new(
        TrackerInstrumentKind.Sample,
        "Sampler",
        "One recording, pitched by resampling.",
        true);

    /// <summary>
    /// The Mother-32 machine: one oscillator blended with noise, a filter that sweeps, an
    /// envelope that is attack and decay, and glide between notes.
    /// </summary>
    /// <remarks>
    /// Monophonic, which is not a limitation here: this engine has always given a track one
    /// voice and cut it when the next note arrives, and that is exactly the arrangement glide
    /// was made for.
    /// </remarks>
    public static readonly Machine Ouroboros = new(
        TrackerInstrumentKind.Ouroboros,
        "Ouroboros",
        "Mono synth. One oscillator, noise, a sweeping filter, and glide.",
        true);

    /// <summary>Somebody else's instrument, hosted: Serum, Vital, anything that takes notes.</summary>
    public static readonly Machine Plugin = new(
        TrackerInstrumentKind.Plugin,
        "Plugin",
        "A VST3 or CLAP instrument, playing in a process of its own.",
        false);

    /// <summary>Every machine there is, in the order they are offered.</summary>
    public static IReadOnlyList<Machine> All { get; } = new[] { OddSkilla, Ouroboros, Sampler, Plugin };

    /// <summary>The ones that are ours to program, as opposed to a plugin we only host.</summary>
    public static IReadOnlyList<Machine> Ours { get; } = All.Where(m => m.IsOurs).ToList();

    /// <summary>Which machine a kind is on. Never null: every kind has one.</summary>
    public static Machine For(TrackerInstrumentKind kind) =>
        All.FirstOrDefault(m => m.Kind == kind) ?? OddSkilla;

    public override string ToString() => Name;
}
