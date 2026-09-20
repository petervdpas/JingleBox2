using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Keeping a preset of your own on a soundmachine, and taking one off.
/// </summary>
/// <remarks>
/// Walked over real folders and a real registry: a machine of this test's own is written into a
/// shipped folder of its own, installed the way the application installs one, and worked on from
/// there, so what is yours and what the machine ships with is answered the way the application
/// answers it. Every unhappy path here is a way of losing a preset somebody made, or of taking
/// one the machine ships with, so those are most of it.
///
/// **The machine is made here and is not one that ships**, which is the whole reason this file
/// stopped breaking. What is on the rack is content: a machine can be added, renamed or taken out
/// of the repository on any afternoon, and a test that leant on one went red for a reason that
/// had nothing to do with what it was asking. It did: Lighttower was removed and fifteen tests
/// about preset naming failed. What ships is tested by the files that are about what ships.
/// </remarks>
public sealed class PresetKeepingTests : IDisposable
{
    /// <summary>An application folder of this test's own.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        public string Name => "JingleBox2";

        public string Path(string appName) => path;

        public string Path() => path;
    }

    /// <summary>Answers the questions the way a test says, and writes down what was asked.</summary>
    private sealed class Answers : IPresetQuestions
    {
        public string? Named { get; set; }

        public bool Replacing { get; set; } = true;

        public bool Deleting { get; set; } = true;

        public List<string> Said { get; } = new();

        public Task<string?> Name(string machine, string suggested, string why = "")
        {
            Said.Add("name " + suggested);

            if (why.Length > 0) Said.Add("why " + why);

            return Task.FromResult(Named);
        }

        public Task<bool> Replace(string name)
        {
            Said.Add("replace " + name);

            return Task.FromResult(Replacing);
        }

        public Task<bool> Delete(string name)
        {
            Said.Add("delete " + name);

            return Task.FromResult(Deleting);
        }

        public Task Refused(string why)
        {
            Said.Add("refused " + why);

            return Task.CompletedTask;
        }
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "jinglebox2-keeping-" + Guid.NewGuid().ToString("N"));

    private readonly SoundMachineRegistry _registry;

    private readonly SoundMachineProjects _projects = new();

    /// <summary>Ships a machine of this test's own beside a fresh application folder, and installs it.</summary>
    public PresetKeepingTests()
    {
        string shipped = Path.Combine(_root, "shipped", "rack", "machines");
        string app = Path.Combine(_root, "app");

        Directory.CreateDirectory(app);
        Write(Path.Combine(shipped, Named));

        _registry = new SoundMachineRegistry(folder: new Somewhere(app), shipped: shipped);
        _projects.Keep(_registry.Load());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.Forget();

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>The machine these tests are run over, as its folder and its id are spelled.</summary>
    private const string Named = "Bench";

    /// <inheritdoc cref="Named"/>
    private const string Id = "machine.bench";

    /// <summary>
    /// The sixteen keys a kit's pads sit on, which is the grid a kit machine draws.
    /// </summary>
    /// <remarks>
    /// Sixteen and not two, because a kit instrument has sixteen pads whatever its face shows,
    /// and a preset only carries the pads the face draws: with a shorter grid the pads past the
    /// end are quietly dropped, which is a kept preset that has lost half its sounds.
    /// </remarks>
    private static readonly string[] PadKeys =
    {
        "C-4", "C#4", "D-4", "D#4", "E-4", "F-4", "F#4", "G-4",
        "G#4", "A-4", "A#4", "B-4", "C-5", "C#5", "D-5", "D#5",
    };

    /// <summary>The names this machine ships with, which is what a preset of yours may not take.</summary>
    /// <remarks>
    /// Three is enough to ask everything here: one to be refused in its own spelling, one to be
    /// refused in another, and a third so a list has something left in it.
    /// </remarks>
    private static readonly string[] Shipping = { "01 Init", "02 Glass", "03 Teeth" };

    /// <summary>
    /// Writes a machine into a folder: a manifest, and the presets it is born with.
    /// </summary>
    /// <remarks>
    /// Its own rather than one off the rack. The rules being asked about here are the library's,
    /// and they are the same whatever machine is under them; leaning on one that ships makes this
    /// file break whenever somebody edits content, which is not what it is about.
    ///
    /// A synth, because the preset it keeps has to carry a patch that can be read back, and the
    /// engine is compiled in where a face is not.
    /// </remarks>
    /// <param name="to">Where the machine goes.</param>
    private static void Write(string to) => Write(to, Id, Named, "Synth", Shipping);

    /// <summary>
    /// Writes a device into a folder: a manifest, and the presets it is born with.
    /// </summary>
    /// <remarks>
    /// One writer for both worlds, since what differs between them is the name of the file at
    /// the top of the folder and the word a preset files itself under. See <see cref="Write(string)"/>
    /// for why these are made here rather than taken off the rack.
    /// </remarks>
    /// <param name="to">Where the device goes.</param>
    /// <param name="id">Its id, which is what a song and a chain write down.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="engine">The engine it plays, which this build has to have.</param>
    /// <param name="presets">The names it ships with.</param>
    /// <param name="effect">True for an effect, which keeps a different manifest and preset key.</param>
    /// <param name="pads">True for a kit, whose preset carries a wave per pad off its own grid.</param>
    private static void Write(string to, string id, string name, string engine,
                              IReadOnlyList<string> presets, bool effect = false, bool pads = false)
    {
        Directory.CreateDirectory(to);

        /* The two controls the tests below vary, declared on the face: a preset carries the keys
           the face names and nothing else, so a machine with no parameters keeps a preset that
           has forgotten the sound. That is the machine's rule and not a thing to work around. */
        const string knobs =
            ",\"Parameters\":[{\"Key\":\"decay\",\"Min\":0,\"Max\":5000,\"Saved\":true},"
            + "{\"Key\":\"release\",\"Min\":0,\"Max\":5000,\"Saved\":true}]";

        /* And the one an effect's preset is asked to carry below. */
        const string dials =
            ",\"Parameters\":[{\"Key\":\"time\",\"Min\":0,\"Max\":2000,\"Saved\":true},"
            + "{\"Key\":\"mix\",\"Min\":0,\"Max\":1,\"Saved\":true}]";

        /* A kit's preset carries a wave per pad, and which pads there are is read off the grid
           the face draws: a kit with no Pads part keeps a preset that has forgotten its sounds. */
        string grid =
            ",\"Panel\":{\"Root\":{\"Element\":\"Column\",\"Children\":"
            + "[{\"Element\":\"Pads\",\"Properties\":{\"rows\":\"4\",\"columns\":\"4\"},\"Children\":["
            + string.Join(",", PadKeys.Select((key, at) =>
                  "{\"Element\":\"Pad\",\"Parameter\":\"pad" + (at + 1) + "\",\"Properties\":{\"key\":\"" + key + "\"}}"))
            + "]}]}}";

        File.WriteAllText(Path.Combine(to, effect ? "effect.json" : "machine.json"),
            "{\"Id\":\"" + id + "\",\"Name\":\"" + name + "\",\"Version\":\"1.0\",\"Engine\":\"" + engine + "\""
            + (effect ? dials : knobs) + (pads ? grid : "") + "}");

        string folder = Path.Combine(to, SoundMachineProject.PresetsFolder);

        Directory.CreateDirectory(folder);

        foreach (string one in presets)
        {
            if (pads)
            {
                /* A kit's shipped preset keeps its own sounds in a folder beside it, named from
                   there, which is how one travels in the machine's zip. */
                string beside = Path.Combine(folder, one);

                Directory.CreateDirectory(beside);
                File.WriteAllBytes(Path.Combine(beside, "Kick.wav"), new byte[] { 8, 8 });

                File.WriteAllText(Path.Combine(folder, one + ".json"),
                    "{\"Name\":\"" + one + "\",\"Machine\":\"" + id + "\","
                    + "\"C-4\":{\"pad_take\":\"" + one + "/Kick.wav\",\"pad_name\":\"Kick\"}}");

                continue;
            }

            File.WriteAllText(Path.Combine(folder, one + ".json"),
                effect
                    ? "{\"Name\":\"" + one + "\",\"Effect\":\"" + id + "\",\"mix\":0.3}"
                    : "{\"Name\":\"" + one + "\",\"Kind\":1,\"MachineId\":\"" + id + "\"}");
        }
    }

    /// <summary>A library over the installed machines, answering what ships by the real registry.</summary>
    private SoundMachinePresets Library() => new(_projects, registry: _registry);

    /// <summary>The installed machine's presets folder.</summary>
    private string Folder => Path.Combine(_projects.For(Id)!.Folder, SoundMachineProject.PresetsFolder);

    /// <summary>An instrument on that machine, with a release nobody ships.</summary>
    /// <remarks>
    /// One number out of the patch is varied and read back, which is the whole of what the
    /// preset has to carry for these: a kept preset that lost the sound would be a preset of
    /// the machine rather than of what somebody made on it.
    /// </remarks>
    private static TrackerInstrument Sound(double release = 1234)
    {
        var instrument = TrackerInstrument.CreateSynth("Mine");

        instrument.MachineId = Id;
        instrument.Patch!.ReleaseMs = release;
        instrument.Patch.DecayMs = 777;

        return instrument;
    }

    /// <summary>A kept preset lands in the installed folder, is marked yours, comes last, and plays what was kept.</summary>
    [Fact]
    public void A_kept_preset_is_yours_and_last()
    {
        var library = Library();
        var sound = Sound();

        /* Counted before anything is kept, since what is kept lands in the same folder. */
        int shipping = Shipping.Length;

        var kept = library.Keep(sound.Machine, sound, "  Teeth of Mine ");

        Assert.NotNull(kept);
        Assert.True(kept!.Yours);
        Assert.Equal("Teeth of Mine", kept.Name);
        Assert.Equal(SoundMachinePreset.YoursMark + "Teeth of Mine", kept.ToString());
        Assert.Equal(Path.Combine(Folder, "Teeth of Mine.json"), kept.File);
        Assert.Equal("Mine", sound.Name);

        var listed = library.For(sound.Machine);

        Assert.Same(listed[^1], kept);
        Assert.Equal(shipping, listed.Count(one => !one.Yours));
        Assert.Equal(1234, kept.Sound.Patch!.ReleaseMs);
        Assert.Equal(777, kept.Sound.Patch.DecayMs);

        var fresh = Library().For(sound.Machine);

        Assert.True(fresh.Single(one => one.Name == "Teeth of Mine").Yours);
    }

    /// <summary>A name that cannot be a file, is nothing, or is one of the machine's own is refused and nothing is written.</summary>
    [Fact]
    public void Names_that_cannot_be_kept_are_refused()
    {
        var library = Library();
        var sound = Sound();
        int before = Directory.GetFiles(Folder).Length;

        /* The last two are the machine's own, in its spelling and in another: a name that ships
           cannot be taken, whichever way it is typed. */
        foreach (string name in new[] { "", "   ", "a/b", "back\\slash", "what?", ".hidden",
                                        Shipping[0], Shipping[1].ToUpperInvariant() })
        {
            Assert.NotEqual("", library.Refusal(sound.Machine, name));
            Assert.Null(library.Keep(sound.Machine, sound, name));
        }

        Assert.Equal(before, Directory.GetFiles(Folder).Length);
        Assert.Equal("", library.Refusal(sound.Machine, "Initial"));
        Assert.NotEqual("", library.Refusal(null, "Anything"));
        Assert.Null(library.Keep(sound.Machine, null!, "Anything"));
    }

    /// <summary>Keeping again under a name of yours replaces it rather than making a second.</summary>
    [Fact]
    public void Keeping_under_your_own_name_replaces_it()
    {
        var library = Library();

        library.Keep(Sound().Machine, Sound(500), "Mine Own");

        Assert.NotNull(library.Yours(Sound().Machine, "MINE OWN"));

        var again = library.Keep(Sound().Machine, Sound(900), "mine own");

        Assert.Single(library.For(Sound().Machine), one => one.Yours);
        Assert.Equal(900, again!.Sound.Patch!.ReleaseMs);
    }

    /// <summary>Only a preset of yours comes off, and one the machine ships with stays however it is asked.</summary>
    [Fact]
    public void Only_yours_can_be_taken_off()
    {
        var library = Library();
        var machine = Sound().Machine;

        var kept = library.Keep(machine, Sound(), "Gone Soon")!;
        var shipped = library.For(machine).First(one => !one.Yours);

        Assert.False(library.Remove(machine, shipped));
        Assert.False(library.Remove(machine, shipped with { Yours = true }));
        Assert.True(File.Exists(shipped.File));

        Assert.True(library.Remove(machine, kept));
        Assert.False(File.Exists(kept.File));
        Assert.DoesNotContain(library.For(machine), one => one.Yours);

        Assert.False(library.Remove(machine, kept));
        Assert.False(library.Remove(null, kept));
        Assert.False(library.Remove(machine, null));
    }

    /// <summary>A preset of yours survives the machine being brought up to date, which reads its folders again.</summary>
    [Fact]
    public void A_preset_of_yours_survives_the_machine_being_updated()
    {
        var kept = Library().Keep(Sound().Machine, Sound(), "Keeper")!;

        _projects.Keep(_registry.Load());

        Assert.True(File.Exists(kept.File));
        Assert.Contains(Library().For(Sound().Machine), one => one.Yours && one.Name == "Keeper");
    }

    /// <summary>What is picked is kept by the instrument, goes into the song with it, and the next face shows it.</summary>
    /// <remarks>
    /// A face is drawn fresh every time a song is opened, so a picker that only remembered for
    /// itself showed nothing picked on every machine in the song.
    /// </remarks>
    [Fact]
    public void The_instrument_keeps_what_was_picked_for_the_next_face()
    {
        var sound = Sound();
        Rack.SoundDevices.Faces.Interfaces.IPanelPresets face = new InstrumentPresets(sound, () => { }, _projects, library: Library());

        face.Picked = 0;

        string shown = face.Names[0];

        Assert.Equal(shown, sound.Preset);

        var reopened = System.Text.Json.JsonSerializer.Deserialize<TrackerInstrument>(
            System.Text.Json.JsonSerializer.Serialize(sound))!;

        Assert.Equal(shown, reopened.Preset);
        Assert.Equal(shown, reopened.Clone().Preset);

        Rack.SoundDevices.Faces.Interfaces.IPanelPresets again = new InstrumentPresets(reopened, () => { }, _projects, library: Library());

        Assert.Equal(0, again.Picked);
    }

    /// <summary>An instrument nobody picked a preset for writes none into the song.</summary>
    [Fact]
    public void An_instrument_on_no_preset_writes_none()
    {
        Assert.DoesNotContain("\"Preset\"", System.Text.Json.JsonSerializer.Serialize(Sound()));
    }

    /// <summary>The Menu's Save asks a name, keeps it, and shows it as picked; a refused name writes nothing.</summary>
    [Fact]
    public async Task Save_on_the_menu_keeps_and_picks_it()
    {
        var sound = Sound();
        var picker = new InstrumentPresets(sound, () => { }, _projects, library: Library());
        var answers = new Answers { Named = Shipping[0] };
        var menu = new PresetMenu(picker, answers);

        Assert.True(menu.Read()[0].Live);
        Assert.False(menu.Read()[1].Live);

        Assert.False(await menu.Save());
        Assert.Contains(answers.Said, one => one.StartsWith("refused", StringComparison.Ordinal));
        Assert.DoesNotContain(picker.Items, one => one.Yours);

        answers.Named = "Mine Now";

        Assert.True(await menu.Save());
        Assert.Equal("Mine Now", picker.PickedYours);
        Assert.True(menu.Read()[1].Live);
        Assert.Equal(SoundMachinePreset.YoursMark + "Mine Now",
            ((Rack.SoundDevices.Faces.Interfaces.IPanelPresets)picker).Names[^1]);
        Assert.Equal("Mine Now", picker.Suggested);
    }

    /// <summary>Saving over a preset of yours is asked about, and a no writes nothing.</summary>
    [Fact]
    public async Task Replacing_is_asked_and_a_no_keeps_the_old_one()
    {
        var picker = new InstrumentPresets(Sound(300), () => { }, _projects, library: Library());
        var answers = new Answers { Named = "Twice" };
        var menu = new PresetMenu(picker, answers);

        Assert.True(await menu.Save());

        var again = new InstrumentPresets(Sound(800), () => { }, _projects, library: Library());
        var second = new PresetMenu(again, answers);

        answers.Replacing = false;

        Assert.False(await second.Save());
        Assert.Contains("replace Twice", answers.Said);
        Assert.Equal(300, Library().Yours(Sound().Machine, "Twice")!.Sound.Patch!.ReleaseMs);

        answers.Replacing = true;

        Assert.True(await second.Save());
        Assert.Equal(800, Library().Yours(Sound().Machine, "Twice")!.Sound.Patch!.ReleaseMs);
    }

    /// <summary>Delete is asked about, a no keeps the file, and the sound on the instrument is untouched.</summary>
    [Fact]
    public async Task Delete_on_the_menu_is_asked_and_leaves_the_sound()
    {
        var sound = Sound(4321);
        var picker = new InstrumentPresets(sound, () => { }, _projects, library: Library());
        var answers = new Answers { Named = "Short Lived" };
        var menu = new PresetMenu(picker, answers);

        await menu.Save();

        string file = picker.Selected!.File;

        answers.Deleting = false;

        Assert.False(await menu.Delete());
        Assert.True(File.Exists(file));

        answers.Deleting = true;

        Assert.True(await menu.Delete());
        Assert.False(File.Exists(file));
        Assert.Null(picker.PickedYours);
        Assert.Equal(4321, sound.Patch!.ReleaseMs);
        Assert.False(menu.Read()[1].Live);
        Assert.False(await menu.Delete());
    }

    /// <summary>A cancelled name box does nothing at all.</summary>
    [Fact]
    public async Task A_cancelled_name_does_nothing()
    {
        var picker = new InstrumentPresets(Sound(), () => { }, _projects, library: Library());
        var answers = new Answers { Named = null };
        var menu = new PresetMenu(picker, answers);

        Assert.False(await menu.Save("Edit wave changes the file"));
        Assert.Contains("why Edit wave changes the file", answers.Said);
        Assert.DoesNotContain(Library().For(Sound().Machine), one => one.Yours);
    }

    /// <summary>An effect keeps where its controls stand as a preset of yours, from the same Menu lines, and only yours come off.</summary>
    /// <remarks>
    /// An effect of this test's own, installed by the real registry, so what it ships with is
    /// known here and a kept one is yours by the same question a soundmachine asks.
    /// </remarks>
    [Fact]
    public async Task An_effect_keeps_presets_of_yours_too()
    {
        string shipped = Path.Combine(_root, "shipped-effects", "rack", "effects");
        string app = Path.Combine(_root, "app-effects");

        Directory.CreateDirectory(app);
        Write(Path.Combine(shipped, "Bench"), "effect.bench", "Bench", "Delay", Shipping, effect: true);

        var registry = new JingleBox2.SoundDevices.SoundEffects.SoundEffectRegistry(folder: new Somewhere(app), shipped: shipped);
        var effect = registry.Load().Single(one => one.Name == "Bench");
        var shelf = new JingleBox2.SoundDevices.SoundEffects.SoundEffectPresets(registry: registry);

        var values = new JingleBox2.SoundDevices.SoundEffects.SoundEffectValues(
            new JingleBox2.SoundDevices.SoundEffects.Delay(48000, effect.Id));

        var picker = new SoundEffectPresetNames(effect, values, shelf);
        var answers = new Answers { Named = Shipping[0] };
        var menu = new PresetMenu(picker, answers);

        Assert.Equal(Shipping.Length, picker.Names.Count);
        Assert.All(shelf.For(effect), one => Assert.False(one.Yours));

        Assert.False(await menu.Save());
        Assert.Contains(answers.Said, one => one.StartsWith("refused", StringComparison.Ordinal));

        picker.Picked = 0;
        Assert.False(menu.Read()[1].Live);
        Assert.False(await menu.Delete());

        values.Set("time", 777);
        answers.Named = "My Echo";

        Assert.True(await menu.Save());
        Assert.Equal("My Echo", picker.PickedYours);
        Assert.Equal(Shipping.Length + 1, picker.Names.Count);
        Assert.Equal(JingleBox2.SoundDevices.SoundEffects.Records.SoundEffectPreset.YoursMark + "My Echo", picker.Names[^1]);
        Assert.Equal(777, shelf.Yours(effect, "my echo")!.Settings["time"]);
        Assert.True(menu.Read()[1].Live);

        Assert.True(await menu.Delete());
        Assert.Null(picker.PickedYours);
        Assert.Equal(Shipping.Length, picker.Names.Count);
        Assert.Equal(777, values.Get("time"));
        Assert.All(shelf.For(effect), one => Assert.True(File.Exists(one.File)));
    }

    /// <summary>
    /// A kit preset naming your recordings exports with them, and the machine imported elsewhere plays them.
    /// </summary>
    /// <remarks>
    /// Chopper as it ships, a preset kept on it naming three takes from outside its folder: two
    /// with the same file name from two chops, and one that is no longer on disc. The zip carries
    /// the two that exist in a folder named after the preset, one each and the second numbered,
    /// and names them relative to the presets folder; the missing one is left as written. The installed preset is not touched.
    /// Imported into another installation, every pad that had a sound resolves to a file that is there.
    /// </remarks>
    [Fact]
    public void Export_carries_the_recordings_a_preset_names()
    {
        string shipped = Path.Combine(_root, "shipped-chop", "rack", "machines");
        string app = Path.Combine(_root, "app-chop");
        string elsewhere = Path.Combine(_root, "elsewhere");
        string takes = Path.Combine(_root, "my recordings");

        Directory.CreateDirectory(app);
        Directory.CreateDirectory(elsewhere);
        Write(Path.Combine(shipped, "Chopper"), "machine.chopper", "Chopper", "Kit",
              new[] { "Energy Beat" }, pads: true);

        string first = Path.Combine(takes, "loop one", "Kick.wav");
        string second = Path.Combine(takes, "loop two", "Kick.wav");
        string gone = Path.Combine(takes, "gone", "Snare.wav");

        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        File.WriteAllBytes(first, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(second, new byte[] { 4, 5, 6, 7 });

        var registry = new SoundMachineRegistry(folder: new Somewhere(app), shipped: shipped);
        var projects = new SoundMachineProjects();

        projects.Keep(registry.Load());

        var chopper = projects.For("machine.chopper")!;
        var kit = TrackerInstrument.CreateKit("Loops");

        kit.MachineId = chopper.Id;
        kit.Kit!.Pads[0].FilePath = first;
        kit.Kit.Pads[1].FilePath = second;
        kit.Kit.Pads[2].FilePath = gone;
        kit.Kit.Pads[3].FilePath = first;

        var kept = new SoundMachinePresets(projects, registry: registry).Keep(kit.Machine, kit, "Loops")!;
        string before = File.ReadAllText(kept.File);
        string zip = Path.Combine(_root, "chopper.zip");

        new SoundMachineArchive(registry).Export(chopper, zip);

        Assert.Equal(before, File.ReadAllText(kept.File));

        using (var read = System.IO.Compression.ZipFile.OpenRead(zip))
        {
            var names = read.Entries.Select(one => one.FullName).ToList();

            Assert.Contains("presets/Loops/Kick.wav", names);
            Assert.Contains("presets/Loops/Kick 2.wav", names);
            Assert.Single(names, one => one.StartsWith("presets/Loops/", StringComparison.Ordinal) && one.EndsWith("Kick.wav", StringComparison.Ordinal));
            Assert.DoesNotContain("presets/Loops/Snare.wav", names);
            Assert.Contains("presets/Energy Beat/Kick.wav", names);

            using var preset = new StreamReader(read.GetEntry("presets/Loops.json")!.Open());
            string inside = preset.ReadToEnd();

            Assert.Contains("\"Loops/Kick.wav\"", inside);
            Assert.Contains("\"Loops/Kick 2.wav\"", inside);
            Assert.DoesNotContain(first.Replace("\\", "\\\\"), inside);
            Assert.Contains("Snare.wav", inside);
        }

        var there = new SoundMachineRegistry(folder: new Somewhere(elsewhere), shipped: Path.Combine(_root, "nothing ships"));
        var arrived = new SoundMachineArchive(there).Import(zip);

        Assert.NotNull(arrived);

        var thereProjects = new SoundMachineProjects();

        thereProjects.Keep(there.Load());

        var loops = new SoundMachinePresets(thereProjects, registry: there).For(kit.Machine).Single(one => one.Name == "Loops");
        var pads = loops.Sound.Kit!.Pads;

        Assert.StartsWith(Path.GetFullPath(elsewhere), pads[0].FilePath);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(pads[0].FilePath));
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, File.ReadAllBytes(pads[1].FilePath));
        Assert.Equal(pads[0].FilePath, pads[3].FilePath);
        Assert.Equal(gone, pads[2].FilePath);
    }

    /// <summary>
    /// Saving a chopped kit copies its pieces and the recording it was chopped from beside the preset,
    /// and the instrument then plays the copies.
    /// </summary>
    /// <remarks>
    /// The shelf keeps its own files. Saving again reuses the copies rather than numbering a second
    /// set, and a copy edited in the preset's folder is still what the preset plays afterwards.
    /// </remarks>
    [Fact]
    public void Saving_a_chopped_kit_keeps_its_waves_and_its_original_beside_it()
    {
        string shipped = Path.Combine(_root, "shipped-keep", "rack", "machines");
        string app = Path.Combine(_root, "app-keep");
        string shelf = Path.Combine(_root, "shelf");

        Directory.CreateDirectory(app);
        Write(Path.Combine(shipped, "Chopper"), "machine.chopper", "Chopper", "Kit",
              new[] { "Energy Beat" }, pads: true);

        string loop = Path.Combine(shelf, "loop.wav");
        string kick = Path.Combine(shelf, "chopped", "loop", "Kick.wav");
        string snare = Path.Combine(shelf, "chopped", "loop", "Snare.wav");

        Directory.CreateDirectory(Path.GetDirectoryName(kick)!);
        File.WriteAllBytes(loop, new byte[] { 9, 9, 9, 9, 9 });
        File.WriteAllBytes(kick, new byte[] { 1 });
        File.WriteAllBytes(snare, new byte[] { 2, 2 });

        var registry = new SoundMachineRegistry(folder: new Somewhere(app), shipped: shipped);
        var projects = new SoundMachineProjects();

        projects.Keep(registry.Load());

        var kit = TrackerInstrument.CreateKit("Loop Kit");

        kit.MachineId = "machine.chopper";
        kit.Kit!.Pads[0].FilePath = kick;
        kit.Kit.Pads[1].FilePath = snare;
        kit.Kit.Source = loop;

        var picker = new InstrumentPresets(kit, () => { }, projects, library: new SoundMachinePresets(projects, registry: registry));

        Assert.True(picker.Keep("Loops"));

        string beside = Path.Combine(projects.For("machine.chopper")!.Folder, SoundMachineProject.PresetsFolder, "Loops");

        Assert.Equal(Path.Combine(beside, "Kick.wav"), kit.Kit.Pads[0].FilePath);
        Assert.Equal(Path.Combine(beside, "Snare.wav"), kit.Kit.Pads[1].FilePath);
        Assert.Equal(Path.Combine(beside, "loop.wav"), kit.Kit.Source);
        Assert.Equal(new byte[] { 9, 9, 9, 9, 9 }, File.ReadAllBytes(kit.Kit.Source));
        Assert.True(File.Exists(kick) && File.Exists(snare) && File.Exists(loop));

        string file = picker.Selected!.File;

        Assert.Contains("\"Loops/loop.wav\"", File.ReadAllText(file));
        Assert.Contains("\"Loops/Kick.wav\"", File.ReadAllText(file));

        File.WriteAllBytes(kit.Kit.Pads[0].FilePath, new byte[] { 7, 7, 7 });

        Assert.True(picker.Keep("Loops"));

        Assert.Equal(Path.Combine(beside, "Kick.wav"), kit.Kit.Pads[0].FilePath);
        Assert.Equal(new byte[] { 7, 7, 7 }, File.ReadAllBytes(kit.Kit.Pads[0].FilePath));
        Assert.Equal(3, Directory.GetFiles(beside).Length);

        var library = new SoundMachinePresets(projects, registry: registry);
        var read = library.For(kit.Machine).Single(one => one.Name == "Loops");

        Assert.Equal(Path.Combine(beside, "loop.wav"), read.Sound.Kit!.Source);

        Assert.True(library.Owns(kit.Machine, kit.Kit.Pads[0].FilePath));
        Assert.False(library.Owns(kit.Machine, kick));
        Assert.False(library.Owns(kit.Machine, library.For(kit.Machine).First(one => !one.Yours).Sound.Kit!.Pads[0].FilePath));
        Assert.False(library.Owns(kit.Machine, Path.Combine(beside, "not there.wav")));
        Assert.False(library.Owns(null, kit.Kit.Pads[0].FilePath));
        Assert.False(library.Owns(kit.Machine, ""));

        var copy = kit.Clone();

        Assert.Equal(kit.Kit.Source, copy.Kit!.Source);
    }

    /// <summary>
    /// RECORD's editor opens on a pad's wave that is not on the shelf, without the name and category a shelf take has.
    /// </summary>
    [Fact]
    public void The_wave_editor_opens_on_a_file_off_the_shelf()
    {
        var bench = new RecorderBench();
        string wave = Path.Combine(_root, "Kick.wav");

        Directory.CreateDirectory(_root);
        File.WriteAllBytes(wave, new byte[64]);

        bench.Page.Edit(wave, "Kick");

        Assert.Equal(wave, bench.Page.SelectedRecordingForEdit!.FilePath);
        Assert.Equal("Kick", bench.Page.EditName);
        Assert.False(bench.Page.EditsShelf);
        Assert.False(bench.Page.CanRename);
        Assert.True(bench.Page.IsEditing);
        Assert.NotEqual(wave, bench.Page.EditingPath);

        bench.Page.EndEdit();

        Assert.Equal(new byte[64], File.ReadAllBytes(wave));

        bench.Page.Edit("", "Nothing");

        Assert.False(bench.Page.IsEditing);
    }
}
