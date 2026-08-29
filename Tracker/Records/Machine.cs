using System.Collections.Generic;
using System.Linq;
using JingleBox2.Machines;
using JingleBox2.Tracker.Enums;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Machines.Records;

namespace JingleBox2.Tracker.Records;

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
/// <param name="Kind">Which engine is behind it, which is what a song's own file says.</param>
/// <param name="Name">What it calls itself, read off its project rather than written here.</param>
/// <param name="Summary">The sentence under its name on the rack, in the machine's own words.</param>
/// <param name="IsOurs">
/// True for a machine of this application's, false for the plugin heading. What
/// <see cref="Forget"/> clears, since the plugin heading is written down here and everything
/// else comes off disc.
/// </param>
/// <param name="Theme">The colours it is painted in, its own and not the app's.</param>
public sealed record Machine(
    TrackerInstrumentKind Kind,
    string Name,
    string Summary,
    bool IsOurs,
    MachineTheme Theme) : IMachine
{
    /// <summary>The machine's own colour, which is where everything it is painted with starts.</summary>
    public string Colour => Theme.Accent;
    /// <summary>
    /// What this machine is called in files, which is the id the contract asks for.
    /// </summary>
    /// <remarks>
    /// The same string a machine's own slot on the rack uses, because they are the same fact:
    /// "machine.zampler" is what a Zampler is, whether it is the box on the shelf or the line
    /// in a song saying which machine an instrument was made on.
    /// </remarks>
    string IMachine.Id => SlotId;

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
        TrackerInstrumentKind.MonoSynth => "machine.ouroboros",
        TrackerInstrumentKind.Kit => "machine.bongabong",
        TrackerInstrumentKind.Sampler => "machine.zampler",
        _ => ""
    };

    /// <summary>
    /// True when that id is a machine's own slot rather than something you made.
    /// </summary>
    /// <remarks>
    /// Asked of every machine there is and not only the installed ones, on purpose. This is what
    /// decides whether a file on the shelf is retired, and a machine thrown out in SETTINGS must
    /// leave its settings exactly where they were for when it is added back.
    /// </remarks>
    public static bool IsSlot(string? id) => !string.IsNullOrEmpty(id) && KindOf(id) is not null;

    /// <summary>
    /// The machine whose slot that is, or null when the id is an ordinary instrument.
    /// </summary>
    /// <remarks>
    /// Answers for a machine that is not installed as well, since the caller is asking what a
    /// file on the shelf is rather than what is on the rack. What comes back is greyed and named
    /// for its engine, which is all anybody knows about a machine that is not here.
    ///
    /// A row to show, and not a thing that can be played. Whether an instrument may sound is a
    /// separate question with its own answer, <see cref="Machines.Interfaces.IMachineProjects.Has"/>,
    /// and it says no for exactly the machines this stands in for.
    /// </remarks>
    public static Machine? SlotFor(string? id) =>
        KindOf(id) is { } kind ? For(kind) : null;

    /// <summary>
    /// A plugin is not a machine project and never will be.
    /// </summary>
    /// <remarks>
    /// The one entry written out here, because there is nothing to read it from: a plugin is
    /// somebody else's, sitting wherever they put it, and what the rack shows for one is a
    /// heading rather than a machine's face. Everything else on the list comes off disc.
    ///
    /// Grey, and deliberately: a plugin is somebody else's box on the rack, and giving it a
    /// colour of its own would say it was one of ours.
    /// </remarks>
    public static readonly Machine Plugin = new(
        TrackerInstrumentKind.Plugin,
        "Plugin",
        "A VST3 or CLAP instrument, playing in a process of its own.",
        false,
        new MachineTheme("#7B838C"));

    /// <summary>
    /// The order machines stand in, which is the app's and not any machine's.
    /// </summary>
    /// <remarks>
    /// Reading order rather than alphabetical: the plainest first and the odd one last, which is
    /// how they were introduced and how anybody learning them meets them. A machine that is not
    /// installed simply is not there, so the rest close up.
    ///
    /// Also the list of engines this build has. An id that is not one of these is a machine
    /// written against a later version, and it is left on the shelf rather than put on the rack
    /// as a box with nothing behind it.
    /// </remarks>
    private static readonly TrackerInstrumentKind[] Offered =
    {
        TrackerInstrumentKind.Synth,
        TrackerInstrumentKind.MonoSynth,
        TrackerInstrumentKind.Sampler,
        TrackerInstrumentKind.Kit,
        TrackerInstrumentKind.Sample,
    };

    /// <summary>
    /// What a machine of that kind is called before anything has been read off disc.
    /// </summary>
    /// <remarks>
    /// Not the machine's name: the machine is what is missing. This is the engine behind it,
    /// which is in the program and is what the song's own file says. A song written where
    /// Zampler was installed and opened where it was not shows a grey "Sampler", which is the
    /// truth: the engine is here and the box it was programmed on is not.
    ///
    /// The engine being here is not the same as the instrument being playable. It is silent
    /// until the machine is back, and greyed for that reason as much as for the name.
    /// </remarks>
    private static string Engine(TrackerInstrumentKind kind) => kind switch
    {
        TrackerInstrumentKind.Synth => "Synth",
        TrackerInstrumentKind.MonoSynth => "Mono synth",
        TrackerInstrumentKind.Sampler => "Sampler",
        TrackerInstrumentKind.Kit => "Kit",
        TrackerInstrumentKind.Sample => "Recording",
        _ => "",
    };

    /// <summary>What the app knows of the machines installed here, plus the plugin heading.</summary>
    private static List<Machine> Registered { get; } = new() { Plugin };

    /// <summary>Every machine there is: the ones installed, in order, and the plugin heading.</summary>
    public static IReadOnlyList<Machine> All => Installed.Append(Plugin).ToList();

    /// <summary>
    /// The machines on the rack: installed here, in the order they are offered.
    /// </summary>
    /// <remarks>
    /// A machine is its project. Without one there is no panel to draw, no presets to offer and
    /// nothing a box on the rack could do, so a machine thrown out in SETTINGS comes off the rack
    /// with it and comes back when it is added again.
    ///
    /// What it left behind stays where it was. The slot file on the shelf is still a machine's,
    /// since <see cref="IsSlot"/> asks the engines and not this, so nothing retires it and adding
    /// the machine back brings the box back with whatever was set on it.
    /// </remarks>
    public static IReadOnlyList<Machine> Installed =>
        Offered.Select(kind => Registered.FirstOrDefault(one => one.Kind == kind))
            .Where(one => one is not null)
            .Select(one => one!)
            .ToList();

    /// <summary>Forgets every machine read off disc, for a list about to be read again.</summary>
    /// <remarks>
    /// Called before the folder is walked rather than after, so a machine thrown out is gone from
    /// the moment the list is rebuilt. Without it a removed machine would keep its place on the
    /// rack until the app was restarted.
    /// </remarks>
    public static void Forget() => Registered.RemoveAll(one => one.IsOurs);

    /// <summary>
    /// Takes a machine that has just been read off disc.
    /// </summary>
    /// <remarks>
    /// Everything the app shows about a machine comes through here: what it is called, what it
    /// says it is, and what colour it wears are the machine's own and are read from its folder.
    /// An id this build has no engine for is refused rather than added as a box that cannot
    /// sound, which is what makes a machines folder from a later version harmless.
    /// </remarks>
    public static bool Register(string id, string name, string summary, MachineTheme theme)
    {
        var kind = KindOf(id);

        if (kind is not { } engine) return false;

        Registered.RemoveAll(one => one.Kind == engine);

        Registered.Add(new Machine(
            engine,
            string.IsNullOrWhiteSpace(name) ? Engine(engine) : name,
            summary ?? "",
            true,
            theme ?? new MachineTheme("#7B838C")));

        return true;
    }

    /// <summary>Which engine that slot id is for, or nothing when this build has none.</summary>
    private static TrackerInstrumentKind? KindOf(string? id) =>
        Offered.Where(kind => new Machine(kind, "", "", true, Bare).SlotId == id)
            .Select(kind => (TrackerInstrumentKind?)kind)
            .FirstOrDefault();

    /// <summary>The colour of a machine that is not here to say what colour it is.</summary>
    private static readonly MachineTheme Bare = new("#7B838C");

    /// <summary>
    /// Which machine a kind is on. Never null: every kind has one.
    /// </summary>
    /// <remarks>
    /// A kind whose machine is not installed answers with the engine behind it, greyed. A song
    /// still holds instruments of that kind and the list they are in still has to write something
    /// beside them; what it must not do is name a machine that is not here.
    ///
    /// Nor may what comes back be taken as permission to play. This names a row; whether the
    /// machine behind it is really installed is
    /// <see cref="Machines.Interfaces.IMachineProjects.Has"/>, and an instrument it says no to is
    /// silent.
    /// </remarks>
    public static Machine For(TrackerInstrumentKind kind) =>
        Registered.FirstOrDefault(one => one.Kind == kind)
        ?? new Machine(kind, Engine(kind), "Not installed here.", kind != TrackerInstrumentKind.Plugin, Bare);

    /// <summary>Its name, so a machine can be dropped straight into a list or a status line.</summary>
    public override string ToString() => Name;
}
