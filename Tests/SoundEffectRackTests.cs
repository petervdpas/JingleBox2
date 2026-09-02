using System;
using System.IO;
using System.Linq;
using JingleBox2.Files.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The effect world with nothing in it: the manifest, the two folders, the offer and the gate.
/// </summary>
/// <remarks>
/// Almost none of this is the happy path, deliberately. What the rules have to survive is a
/// folder that is not an effect, a manifest somebody edited into nonsense, an id this build has
/// no engine for, and an installation where somebody has thrown things out. Those only ever show
/// on another person's disc, which is exactly why they are written down here.
/// </remarks>
public class SoundEffectRackTests : IDisposable
{
    /// <summary>A temporary folder standing in for the application's own.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        /// <inheritdoc/>
        public string Name => "JingleBox2";

        /// <inheritdoc/>
        public string Path() => path;

        /// <inheritdoc/>
        public string Path(string appName) => path;
    }

    /// <summary>An engine list that knows exactly the ids it was told about.</summary>
    private sealed class Knows(params string[] ids) : ISoundEffectEngines
    {
        /// <inheritdoc/>
        public bool Has(string? id) =>
            id is { Length: > 0 } && ids.Contains(id, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public ISoundEffectEngine? Make(string? id, int sampleRate, int maxFrames) => null;
    }

    /// <summary>This test's own corner of the disc, thrown away afterwards.</summary>
    private readonly string _root =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jb2-effects-" + Guid.NewGuid().ToString("N"));

    /// <summary>Where the effects that ship pretend to be.</summary>
    private string Shipped => System.IO.Path.Combine(_root, "beside", "effects");

    /// <summary>And where this pretend installation keeps its own, under the rack folder.</summary>
    private string Installed => System.IO.Path.Combine(_root, "app", "rack", "effects");

    /// <summary>Where they sat before the two worlds were kept together.</summary>
    private string Was => System.IO.Path.Combine(_root, "app", "effects");

    /// <summary>A registry pointed at this test's folders.</summary>
    private SoundEffectRegistry Registry(params string[] engines) =>
        new(new Knows(engines), folder: new Somewhere(System.IO.Path.Combine(_root, "app")), shipped: Shipped);

    /// <summary>Writes an effect's folder with a manifest in it.</summary>
    private static string Effect(string under, string folder, string id, string name = "Echo")
    {
        string where = System.IO.Path.Combine(under, folder);

        new SoundEffectProject { Id = id, Name = name, Summary = "One line." }.Save(where);

        return where;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>A folder in the effects folder that holds no manifest is not an effect.</summary>
    [Fact]
    public void A_folder_with_no_manifest_is_not_an_effect()
    {
        Directory.CreateDirectory(System.IO.Path.Combine(Installed, "notes"));

        Assert.Empty(Registry().In(Installed));
    }

    /// <summary>A manifest somebody edited into nonsense costs that one folder and not the rack.</summary>
    [Fact]
    public void A_manifest_that_will_not_parse_is_passed_over()
    {
        string where = System.IO.Path.Combine(Installed, "broken");

        Directory.CreateDirectory(where);
        File.WriteAllText(System.IO.Path.Combine(where, SoundEffectProject.ManifestName), "{ this is not json");

        Assert.Empty(Registry().In(Installed));
        Assert.Null(SoundEffectProject.Open(where));
    }

    /// <summary>An effect with no id is nothing: an id is what a chain writes down.</summary>
    [Fact]
    public void A_manifest_with_no_id_is_passed_over()
    {
        Effect(Installed, "nameless", "");

        Assert.Empty(Registry().In(Installed));
    }

    /// <summary>No folder and no path both read as no effect.</summary>
    [Fact]
    public void Opening_nothing_is_nothing_rather_than_a_fault()
    {
        Assert.Null(SoundEffectProject.Open(""));
        Assert.Null(SoundEffectProject.Open(System.IO.Path.Combine(_root, "never", "here")));
    }

    /// <summary>The manifest round trips, and saving makes the folders an effect always has.</summary>
    [Fact]
    public void A_project_saved_reads_back_as_it_was_written()
    {
        string where = System.IO.Path.Combine(_root, "work", "Echo");

        var made = new SoundEffectProject
        {
            Id = "effect.echo",
            Name = "Echo",
            Summary = "A delay.",
            Author = "Somebody",
            Version = "2.0",
            Theme = new PanelTheme("#123456"),
        };

        made.Parameters.Add(new Parameter { Key = "mix", Name = "Mix", Min = 0, Max = 1, Default = 0.3 });
        made.Save(where);

        var read = SoundEffectProject.Open(where);

        Assert.NotNull(read);
        Assert.Equal("effect.echo", read!.Id);
        Assert.Equal("Echo", read.Name);
        Assert.Equal("2.0", read.Version);
        Assert.Equal("#123456", read.Colour);
        Assert.Equal("mix", Assert.Single(read.Parameters).Key);
        Assert.Equal(where, read.Folder);
        Assert.True(Directory.Exists(System.IO.Path.Combine(where, SoundEffectProject.PresetsFolder)));
        Assert.True(Directory.Exists(System.IO.Path.Combine(where, SoundEffectProject.ImagesFolder)));
    }

    /// <summary>Read is not taken: the folder is read and the gate decides.</summary>
    [Fact]
    public void An_id_with_no_engine_is_read_and_left_off_the_rack()
    {
        Effect(Installed, "Echo", "effect.echo");
        Effect(Installed, "Later", "effect.written.later");

        var registry = Registry("effect.echo");

        Assert.Equal(2, registry.In(Installed).Count);

        var taken = registry.Load();

        Assert.Equal("effect.echo", Assert.Single(taken).Id);
    }

    /// <summary>Which is today's state of it, and is why the tab is empty.</summary>
    [Fact]
    public void With_no_engines_at_all_nothing_reaches_the_rack()
    {
        Effect(Installed, "Echo", "effect.echo");

        Assert.Empty(Registry().Load());
    }

    /// <summary>The list that ships knows what it was told about and nothing else.</summary>
    /// <remarks>
    /// An id nobody has written an engine for is the ordinary case for anything somebody else
    /// made, and it has to be a plain no rather than a fault.
    /// </remarks>
    [Fact]
    public void The_engine_list_answers_only_for_what_it_has()
    {
        var engines = new SoundEffectEngines();

        Assert.True(engines.Has(SoundEffectEngines.EchoBox));
        Assert.False(engines.Has("effect.echo"));
        Assert.False(engines.Has(""));
        Assert.False(engines.Has(null));
        Assert.Null(engines.Make("effect.echo", 48000, 512));
    }

    /// <summary>A first run is offered everything that ships, and the offer is written down.</summary>
    /// <remarks>
    /// Landing in a folder named after its id rather than after the folder it shipped in, which
    /// is what the archive does for a machine and for the same reason: an id is the one name that
    /// cannot collide with somebody else's box by accident.
    /// </remarks>
    [Fact]
    public void A_shipped_effect_never_offered_is_taken()
    {
        Effect(Shipped, "Echo", "effect.echo");

        var taken = Registry("effect.echo").Load();

        Assert.Equal("effect.echo", Assert.Single(taken).Id);
        Assert.True(File.Exists(System.IO.Path.Combine(Installed, "effect.echo", SoundEffectProject.ManifestName)));
        Assert.True(File.Exists(System.IO.Path.Combine(Installed, "offered.txt")));
    }

    /// <summary>Having been offered is what is remembered, so a deletion is not undone on the next start.</summary>
    [Fact]
    public void One_thrown_out_stays_thrown_out()
    {
        Effect(Shipped, "Echo", "effect.echo");

        Registry("effect.echo").Load();

        Directory.Delete(System.IO.Path.Combine(Installed, "effect.echo"), recursive: true);

        Assert.Empty(Registry("effect.echo").Load());
        Assert.False(Directory.Exists(System.IO.Path.Combine(Installed, "effect.echo")));
    }

    /// <summary>The folder existing is not the test, which is the fault the offer file was written for.</summary>
    [Fact]
    public void One_written_after_the_folder_was_made_still_arrives()
    {
        Effect(Shipped, "Echo", "effect.echo");

        Registry("effect.echo").Load();

        Effect(Shipped, "Room", "effect.room");

        var taken = Registry("effect.echo", "effect.room").Load();

        Assert.Equal(new[] { "effect.echo", "effect.room" }, taken.Select(one => one.Id).OrderBy(one => one).ToArray());
    }

    /// <summary>What ships is brought up to date by the clock, and a preset of yours survives it.</summary>
    [Fact]
    public void A_newer_shipped_file_is_copied_over_and_nothing_of_yours_is_deleted()
    {
        string from = Effect(Shipped, "Echo", "effect.echo", "Echo");

        Registry("effect.echo").Load();

        string mine = System.IO.Path.Combine(Installed, "effect.echo");
        string preset = System.IO.Path.Combine(mine, SoundEffectProject.PresetsFolder, "mine.json");

        File.WriteAllText(preset, "{}");

        new SoundEffectProject { Id = "effect.echo", Name = "Echo mkII", Summary = "One line." }.Save(from);
        File.SetLastWriteTimeUtc(
            System.IO.Path.Combine(from, SoundEffectProject.ManifestName), DateTime.UtcNow.AddMinutes(5));

        var taken = Registry("effect.echo").Load();

        Assert.Equal("Echo mkII", Assert.Single(taken).Name);
        Assert.True(File.Exists(preset));
    }

    /// <summary>Edited here and newer means yours, so it is not overwritten by what ships.</summary>
    [Fact]
    public void A_file_of_yours_that_is_newer_is_left_alone()
    {
        string from = Effect(Shipped, "Echo", "effect.echo", "Echo");

        Registry("effect.echo").Load();

        string mine = System.IO.Path.Combine(Installed, "effect.echo", SoundEffectProject.ManifestName);

        new SoundEffectProject { Id = "effect.echo", Name = "Mine", Summary = "Edited here." }
            .Save(System.IO.Path.Combine(Installed, "effect.echo"));

        File.SetLastWriteTimeUtc(mine, DateTime.UtcNow.AddMinutes(5));
        File.SetLastWriteTimeUtc(System.IO.Path.Combine(from, SoundEffectProject.ManifestName), DateTime.UtcNow);

        Assert.Equal("Mine", Assert.Single(Registry("effect.echo").Load()).Name);
    }

    /// <summary>What is on offer is what ships and is not here.</summary>
    [Fact]
    public void What_ships_and_is_not_installed_is_what_is_available()
    {
        Effect(Shipped, "Echo", "effect.echo");
        Effect(Shipped, "Room", "effect.room");
        Effect(Installed, "Echo", "effect.echo");

        Assert.Equal("effect.room", Assert.Single(Registry().Available()).Id);
    }

    /// <summary>Asked by looking rather than by where the path points.</summary>
    [Fact]
    public void Ships_says_whether_the_same_file_is_beside_the_program()
    {
        Effect(Shipped, "Echo", "effect.echo");
        Effect(Installed, "effect.echo", "effect.echo");
        Effect(Installed, "Mine", "effect.mine");

        var registry = Registry();

        Assert.True(registry.Ships(System.IO.Path.Combine(Installed, "effect.echo", SoundEffectProject.ManifestName)));
        Assert.False(registry.Ships(System.IO.Path.Combine(Installed, "Mine", SoundEffectProject.ManifestName)));
        Assert.False(registry.Ships(System.IO.Path.Combine(Shipped, "Echo", SoundEffectProject.ManifestName)));
        Assert.False(registry.Ships(""));
    }

    /// <summary>A folder that does not exist reads as no effects.</summary>
    [Fact]
    public void Reading_a_folder_that_is_not_there_is_nothing_rather_than_a_fault() =>
        Assert.Empty(Registry().In(System.IO.Path.Combine(_root, "never", "here")));

    /// <summary>An id is matched without case, and one with no id is not kept.</summary>
    [Fact]
    public void What_was_found_is_asked_by_id_whatever_the_case()
    {
        var held = new SoundEffectProjects();

        held.Keep(new[]
        {
            new SoundEffectProject { Id = "effect.echo", Name = "Echo" },
            new SoundEffectProject { Id = "", Name = "Nameless" },
        });

        Assert.True(held.Has("EFFECT.ECHO"));
        Assert.Equal("Echo", held.For("effect.echo")!.Name);
        Assert.False(held.Has("effect.room"));
        Assert.False(held.Has(""));
        Assert.False(held.Has(null));
        Assert.Null(held.For("effect.room"));
        Assert.Single(held.All);
    }

    /// <summary>
    /// What an installation already had is carried into the rack folder rather than left behind.
    /// </summary>
    /// <remarks>
    /// The fault this exists to stop: the folder moved, so the new place is empty, so everything
    /// shipped is offered again and what somebody had edited sits under a name nothing reads.
    /// </remarks>
    [Fact]
    public void What_was_already_here_is_carried_into_the_rack_folder()
    {
        Effect(Was, "Echo", "effect.echo", "Mine");

        var taken = Registry("effect.echo").Load();

        Assert.Equal("Mine", Assert.Single(taken).Name);
        Assert.True(File.Exists(System.IO.Path.Combine(Installed, "Echo", SoundEffectProject.ManifestName)));
        Assert.False(Directory.Exists(Was));
    }

    /// <summary>And only once: a folder somebody has since worked in is not argued with.</summary>
    [Fact]
    public void The_old_folder_is_left_alone_once_there_is_a_new_one()
    {
        Effect(Installed, "Echo", "effect.echo", "New place");
        Effect(Was, "Echo", "effect.echo", "Old place");

        Assert.Equal("New place", Assert.Single(Registry("effect.echo").Load()).Name);
        Assert.True(Directory.Exists(Was));
    }

    /// <summary>
    /// A row on the rack shows the effect's own face, on a bench of its own.
    /// </summary>
    /// <remarks>
    /// The face and the bench are made once and handed back as they are: the panel redraws when
    /// it is given a different face, so a row that built a new one on every read would redraw for
    /// ever. What the knobs stand at starts where the effect says, and goes nowhere: an effect in
    /// use is a slot on a track's chain, and this is where you look at one and point a controller
    /// at it.
    /// </remarks>
    [Fact]
    public void A_row_shows_the_effect_on_a_bench_of_its_own()
    {
        var effect = new SoundEffectProject { Id = "effect.echo", Name = "Echo" };

        effect.Parameters.Add(new Parameter { Key = "time", Name = "Time", Min = 10, Max = 2000, Default = 375 });

        var row = new ViewModels.RackSoundEffect(effect);

        Assert.Same(row.Shown, row.Shown);
        Assert.Same(row.Values, row.Values);
        Assert.Same(row.Menu, row.Menu);

        Assert.Equal(375, row.Values.Get("time"), 5);

        row.Values.Set("time", 500);

        Assert.Equal(500, row.Values.Get("time"), 5);
        Assert.Equal(375, effect.Parameters[0].Default, 5);
    }

    /// <summary>And it carries the same menu a machine's face does, keyed by the effect.</summary>
    /// <remarks>
    /// With no desk behind it there is nothing to offer, not even the learning line, which is
    /// the rule <c>ControlMenu</c> already keeps and <c>Tests/SoundMachineMenuTests.cs</c> says in
    /// full: a page with no hardware to point at offers nothing rather than a line that would do
    /// nothing. What is worth saying here is that the row has one at all and hands the same one
    /// back, since the panel is redrawn when it is given a different menu.
    /// </remarks>
    [Fact]
    public void A_row_carries_a_menu_of_its_own()
    {
        var row = new ViewModels.RackSoundEffect(new SoundEffectProject { Id = "effect.echo", Name = "Echo" });

        Assert.NotNull(row.Menu);
        Assert.Empty(row.Menu.Read());
    }

    /// <summary>The list is what was last read, not everything ever read.</summary>
    [Fact]
    public void Keeping_a_new_list_forgets_the_one_before_it()
    {
        var held = new SoundEffectProjects();

        held.Keep(new[] { new SoundEffectProject { Id = "effect.echo" } });
        held.Keep(new[] { new SoundEffectProject { Id = "effect.room" } });

        Assert.False(held.Has("effect.echo"));
        Assert.True(held.Has("effect.room"));
    }
}
