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
    /// The instrument id a machine's own slot in the rack uses.
    /// </summary>
    /// <remarks>
    /// Every machine of ours is always on the shelf, once, under its own name. You do not add
    /// one and you cannot rename or delete one, the way a rack has the boxes it has. So each
    /// needs an id that is the same on every machine and every run, rather than the fresh guid
    /// an instrument you made gets.
    ///
    /// Written out one by one rather than made from the name, so the strings that end up in
    /// people's instrument files can be found by looking for them.
    /// </remarks>
    public string SlotId => Kind switch
    {
        TrackerInstrumentKind.Synth => "machine.oddskilla",
        TrackerInstrumentKind.Sample => "machine.recording",
        TrackerInstrumentKind.Ouroboros => "machine.ouroboros",
        TrackerInstrumentKind.BongaBong => "machine.bongabong",
        TrackerInstrumentKind.Zampler => "machine.zampler",
        _ => ""
    };

    /// <summary>True when that id is a machine's own slot rather than something you made.</summary>
    public static bool IsSlot(string? id) =>
        !string.IsNullOrEmpty(id) && Ours.Any(m => m.SlotId == id);

    /// <summary>The machine whose slot that is, or null when the id is an ordinary instrument.</summary>
    public static Machine? SlotFor(string? id) =>
        string.IsNullOrEmpty(id) ? null : Ours.FirstOrDefault(m => m.SlotId == id);

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
    /// One of your recordings, played back at a pitch: the raw form of Zampler and BongaBong.
    /// </summary>
    /// <remarks>
    /// One file, resampled, through the same envelope and filter OddSkilla uses. This is what
    /// the tracker played recordings with before there were machines at all, and it is the bare
    /// engine both sampling machines are made of rather than a stand-in for either of them.
    ///
    /// What they add to it is a zone map. Zampler spreads recordings across the keyboard and
    /// transposes each one over its range; BongaBong puts one on every key and transposes none
    /// of them. Both are this, plus a list of these and the notes each answers to, so building
    /// them is building the map rather than building the playback.
    ///
    /// Not called "Sampler", which reads as the thing that makes recordings. That is the RECORD
    /// tab; this only plays what comes out of it.
    /// </remarks>
    public static readonly Machine Recording = new(
        TrackerInstrumentKind.Sample,
        "Recording",
        "One of your recordings, pitched by resampling.",
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

    /// <summary>
    /// The kit machine: sixteen pads, one recording to a key, and none of them transposed.
    /// </summary>
    /// <remarks>
    /// The recording machine with a map in front of it. A key here chooses which recording
    /// sounds rather than how fast to read one, because a snare played four semitones up is
    /// not a snare.
    ///
    /// It is also the one machine that does not take the tracker's one voice to a track. A
    /// crash has to go on ringing under the snare that follows it, so its pads sound over each
    /// other; the only thing that cuts a pad is another pad in its choke group, which is what
    /// a closed hihat does to an open one.
    /// </remarks>
    public static readonly Machine BongaBong = new(
        TrackerInstrumentKind.BongaBong,
        "BongaBong",
        "A kit. Sixteen pads, one recording to a key, sounding over each other.",
        true);

    /// <summary>
    /// The sampling machine: recordings laid across the keyboard, each transposed from its root.
    /// </summary>
    /// <remarks>
    /// The recording machine with a map in front of it, the same way BongaBong is, and the two
    /// differ in one line: a pad passes the played note as its own root so nothing moves, and a
    /// zone passes the note it was recorded at so everything does. A piano sampled every fourth
    /// key is thirteen zones, each covering the keys either side of its own.
    ///
    /// Named for the Emulator, which is where the idea comes from: a keyboard is a map, and
    /// what a key does is look itself up on it.
    /// </remarks>
    public static readonly Machine Zampler = new(
        TrackerInstrumentKind.Zampler,
        "Zampler",
        "Recordings across the keyboard. Each zone has a range and a root to transpose from.",
        true);

    /// <summary>Somebody else's instrument, hosted: Serum, Vital, anything that takes notes.</summary>
    public static readonly Machine Plugin = new(
        TrackerInstrumentKind.Plugin,
        "Plugin",
        "A VST3 or CLAP instrument, playing in a process of its own.",
        false);

    /// <summary>Every machine there is, in the order they are offered.</summary>
    public static IReadOnlyList<Machine> All { get; } = new[] { OddSkilla, Ouroboros, Zampler, BongaBong, Recording, Plugin };

    /// <summary>The ones that are ours to program, as opposed to a plugin we only host.</summary>
    public static IReadOnlyList<Machine> Ours { get; } = All.Where(m => m.IsOurs).ToList();

    /// <summary>Which machine a kind is on. Never null: every kind has one.</summary>
    public static Machine For(TrackerInstrumentKind kind) =>
        All.FirstOrDefault(m => m.Kind == kind) ?? OddSkilla;

    public override string ToString() => Name;
}
