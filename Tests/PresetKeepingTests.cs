using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Keeping a preset of your own on a soundmachine, and taking one off.
/// </summary>
/// <remarks>
/// Walked over real folders: Lighttower as it ships, copied beside a test's own application
/// folder and installed by the real registry, so what is yours and what the machine ships with is
/// answered the way the application answers it. Every unhappy path here is a way of losing a
/// preset somebody made, or of taking one the machine ships with, so those are most of it.
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

        public Task<string?> Name(string machine, string suggested)
        {
            Said.Add("name " + suggested);

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

    /// <summary>Ships Lighttower beside a fresh application folder and installs it.</summary>
    public PresetKeepingTests()
    {
        string shipped = Path.Combine(_root, "shipped", "rack", "machines");
        string app = Path.Combine(_root, "app");

        Directory.CreateDirectory(app);
        Copy(Real(), Path.Combine(shipped, "Lighttower"));

        _registry = new SoundMachineRegistry(folder: new Somewhere(app), shipped: shipped);
        _projects.Keep(_registry.Load());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.Forget();

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>The Lighttower that ships with this checkout.</summary>
    private static string Real()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "machines", "Lighttower"))) at = at.Parent;

        return Path.Combine(at!.FullName, "rack", "machines", "Lighttower");
    }

    /// <summary>A folder and everything in it.</summary>
    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string folder in Directory.GetDirectories(from)) Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
    }

    /// <summary>A library over the installed machines, answering what ships by the real registry.</summary>
    private SoundMachinePresets Library() => new(_projects, registry: _registry);

    /// <summary>The installed Lighttower's presets folder.</summary>
    private string Folder => Path.Combine(_projects.For("machine.lighttower")!.Folder, SoundMachineProject.PresetsFolder);

    /// <summary>An instrument on Lighttower, with a sweep nobody ships.</summary>
    private static TrackerInstrument Sound(double sweep = 1234)
    {
        var instrument = TrackerInstrument.CreateSegments("Mine");

        instrument.MachineId = "machine.lighttower";
        instrument.Segments!.SweepMs = sweep;
        instrument.Segments.Motion = SegmentMotion.Bounce;

        return instrument;
    }

    /// <summary>A kept preset lands in the installed folder, is marked yours, comes last, and plays what was kept.</summary>
    [Fact]
    public void A_kept_preset_is_yours_and_last()
    {
        var library = Library();
        var sound = Sound();

        var kept = library.Keep(sound.Machine, sound, "  Teeth of Mine ");

        Assert.NotNull(kept);
        Assert.True(kept!.Yours);
        Assert.Equal("Teeth of Mine", kept.Name);
        Assert.Equal(SoundMachinePreset.YoursMark + "Teeth of Mine", kept.ToString());
        Assert.Equal(Path.Combine(Folder, "Teeth of Mine.json"), kept.File);
        Assert.Equal("Mine", sound.Name);

        var listed = library.For(sound.Machine);

        Assert.Same(listed[^1], kept);
        Assert.Equal(22, listed.Count(one => !one.Yours));
        Assert.Equal(1234, kept.Sound.Segments!.SweepMs);
        Assert.Equal(SegmentMotion.Bounce, kept.Sound.Segments.Motion);

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

        foreach (string name in new[] { "", "   ", "a/b", "back\\slash", "what?", ".hidden", "init", "01 Init", "Glass Organ" })
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

        library.Keep(Sound().Machine, Sound(500), "Pad");

        Assert.NotNull(library.Yours(Sound().Machine, "PAD"));

        var again = library.Keep(Sound().Machine, Sound(900), "pad");

        Assert.Single(library.For(Sound().Machine), one => one.Yours);
        Assert.Equal(900, again!.Sound.Segments!.SweepMs);
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

    /// <summary>The Menu's Save asks a name, keeps it, and shows it as picked; a refused name writes nothing.</summary>
    [Fact]
    public async Task Save_on_the_menu_keeps_and_picks_it()
    {
        var sound = Sound();
        var picker = new InstrumentPresets(sound, () => { }, _projects, library: Library());
        var answers = new Answers { Named = "Init" };
        var menu = new PresetMenu(picker, answers);

        Assert.True(menu.Read()[0].Live);
        Assert.False(menu.Read()[1].Live);

        Assert.False(await menu.Save());
        Assert.Contains(answers.Said, one => one.StartsWith("refused", StringComparison.Ordinal));
        Assert.DoesNotContain(picker.Items, one => one.Yours);

        answers.Named = "Mine Now";

        Assert.True(await menu.Save());
        Assert.Equal("Mine Now", picker.PickedYours?.Name);
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
        Assert.Equal(300, Library().Yours(Sound().Machine, "Twice")!.Sound.Segments!.SweepMs);

        answers.Replacing = true;

        Assert.True(await second.Save());
        Assert.Equal(800, Library().Yours(Sound().Machine, "Twice")!.Sound.Segments!.SweepMs);
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

        string file = picker.PickedYours!.File;

        answers.Deleting = false;

        Assert.False(await menu.Delete());
        Assert.True(File.Exists(file));

        answers.Deleting = true;

        Assert.True(await menu.Delete());
        Assert.False(File.Exists(file));
        Assert.Null(picker.PickedYours);
        Assert.Equal(4321, sound.Segments!.SweepMs);
        Assert.False(menu.Read()[1].Live);
        Assert.False(await menu.Delete());
    }

    /// <summary>A cancelled name box does nothing at all.</summary>
    [Fact]
    public async Task A_cancelled_name_does_nothing()
    {
        var picker = new InstrumentPresets(Sound(), () => { }, _projects, library: Library());
        var menu = new PresetMenu(picker, new Answers { Named = null });

        Assert.False(await menu.Save());
        Assert.DoesNotContain(Library().For(Sound().Machine), one => one.Yours);
    }
}
