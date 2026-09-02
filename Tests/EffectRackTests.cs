using System;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Files.Interfaces;
using JingleBox2.Rack.Faces;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Tracker.Effects;
using JingleBox2.Tracker.Effects.Interfaces;
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
public class EffectRackTests : IDisposable
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
    private sealed class Knows(params string[] ids) : IEffectEngines
    {
        /// <inheritdoc/>
        public bool Has(string? id) =>
            id is { Length: > 0 } && ids.Contains(id, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public IAudioInsert? Make(string? id, int sampleRate, int maxFrames) => null;
    }

    /// <summary>This test's own corner of the disc, thrown away afterwards.</summary>
    private readonly string _root =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jb2-effects-" + Guid.NewGuid().ToString("N"));

    /// <summary>Where the effects that ship pretend to be.</summary>
    private string Shipped => System.IO.Path.Combine(_root, "beside", "effects");

    /// <summary>And where this pretend installation keeps its own.</summary>
    private string Installed => System.IO.Path.Combine(_root, "app", "effects");

    /// <summary>A registry pointed at this test's folders.</summary>
    private EffectRegistry Registry(params string[] engines) =>
        new(new Knows(engines), folder: new Somewhere(System.IO.Path.Combine(_root, "app")), shipped: Shipped);

    /// <summary>Writes an effect's folder with a manifest in it.</summary>
    private static string Effect(string under, string folder, string id, string name = "Echo")
    {
        string where = System.IO.Path.Combine(under, folder);

        new EffectProject { Id = id, Name = name, Summary = "One line." }.Save(where);

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
        File.WriteAllText(System.IO.Path.Combine(where, EffectProject.ManifestName), "{ this is not json");

        Assert.Empty(Registry().In(Installed));
        Assert.Null(EffectProject.Open(where));
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
        Assert.Null(EffectProject.Open(""));
        Assert.Null(EffectProject.Open(System.IO.Path.Combine(_root, "never", "here")));
    }

    /// <summary>The manifest round trips, and saving makes the folders an effect always has.</summary>
    [Fact]
    public void A_project_saved_reads_back_as_it_was_written()
    {
        string where = System.IO.Path.Combine(_root, "work", "Echo");

        var made = new EffectProject
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

        var read = EffectProject.Open(where);

        Assert.NotNull(read);
        Assert.Equal("effect.echo", read!.Id);
        Assert.Equal("Echo", read.Name);
        Assert.Equal("2.0", read.Version);
        Assert.Equal("#123456", read.Colour);
        Assert.Equal("mix", Assert.Single(read.Parameters).Key);
        Assert.Equal(where, read.Folder);
        Assert.True(Directory.Exists(System.IO.Path.Combine(where, EffectProject.PresetsFolder)));
        Assert.True(Directory.Exists(System.IO.Path.Combine(where, EffectProject.ImagesFolder)));
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

    /// <summary>The list that ships knows no id, and says so rather than throwing.</summary>
    [Fact]
    public void The_real_engine_list_has_nothing_in_it_yet()
    {
        var engines = new EffectEngines();

        Assert.False(engines.Has("effect.echo"));
        Assert.False(engines.Has(""));
        Assert.False(engines.Has(null));
        Assert.Null(engines.Make("effect.echo", 48000, 512));
    }

    /// <summary>A first run is offered everything that ships, and the offer is written down.</summary>
    [Fact]
    public void A_shipped_effect_never_offered_is_taken()
    {
        Effect(Shipped, "Echo", "effect.echo");

        var taken = Registry("effect.echo").Load();

        Assert.Equal("effect.echo", Assert.Single(taken).Id);
        Assert.True(File.Exists(System.IO.Path.Combine(Installed, "Echo", EffectProject.ManifestName)));
        Assert.True(File.Exists(System.IO.Path.Combine(Installed, "offered.txt")));
    }

    /// <summary>Having been offered is what is remembered, so a deletion is not undone on the next start.</summary>
    [Fact]
    public void One_thrown_out_stays_thrown_out()
    {
        Effect(Shipped, "Echo", "effect.echo");

        Registry("effect.echo").Load();

        Directory.Delete(System.IO.Path.Combine(Installed, "Echo"), recursive: true);

        Assert.Empty(Registry("effect.echo").Load());
        Assert.False(Directory.Exists(System.IO.Path.Combine(Installed, "Echo")));
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

        string mine = System.IO.Path.Combine(Installed, "Echo");
        string preset = System.IO.Path.Combine(mine, EffectProject.PresetsFolder, "mine.json");

        File.WriteAllText(preset, "{}");

        new EffectProject { Id = "effect.echo", Name = "Echo mkII", Summary = "One line." }.Save(from);
        File.SetLastWriteTimeUtc(
            System.IO.Path.Combine(from, EffectProject.ManifestName), DateTime.UtcNow.AddMinutes(5));

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

        string mine = System.IO.Path.Combine(Installed, "Echo", EffectProject.ManifestName);

        new EffectProject { Id = "effect.echo", Name = "Mine", Summary = "Edited here." }
            .Save(System.IO.Path.Combine(Installed, "Echo"));

        File.SetLastWriteTimeUtc(mine, DateTime.UtcNow.AddMinutes(5));
        File.SetLastWriteTimeUtc(System.IO.Path.Combine(from, EffectProject.ManifestName), DateTime.UtcNow);

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
        Effect(Installed, "Echo", "effect.echo");
        Effect(Installed, "Mine", "effect.mine");

        var registry = Registry();

        Assert.True(registry.Ships(System.IO.Path.Combine(Installed, "Echo", EffectProject.ManifestName)));
        Assert.False(registry.Ships(System.IO.Path.Combine(Installed, "Mine", EffectProject.ManifestName)));
        Assert.False(registry.Ships(System.IO.Path.Combine(Shipped, "Echo", EffectProject.ManifestName)));
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
        var held = new EffectProjects();

        held.Keep(new[]
        {
            new EffectProject { Id = "effect.echo", Name = "Echo" },
            new EffectProject { Id = "", Name = "Nameless" },
        });

        Assert.True(held.Has("EFFECT.ECHO"));
        Assert.Equal("Echo", held.For("effect.echo")!.Name);
        Assert.False(held.Has("effect.room"));
        Assert.False(held.Has(""));
        Assert.False(held.Has(null));
        Assert.Null(held.For("effect.room"));
        Assert.Single(held.All);
    }

    /// <summary>The list is what was last read, not everything ever read.</summary>
    [Fact]
    public void Keeping_a_new_list_forgets_the_one_before_it()
    {
        var held = new EffectProjects();

        held.Keep(new[] { new EffectProject { Id = "effect.echo" } });
        held.Keep(new[] { new EffectProject { Id = "effect.room" } });

        Assert.False(held.Has("effect.echo"));
        Assert.True(held.Has("effect.room"));
    }
}
