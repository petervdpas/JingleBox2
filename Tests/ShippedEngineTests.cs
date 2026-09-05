using System;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.SoundDevices.SoundMachines.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Every device that ships names the engine it plays, in its own manifest.
/// </summary>
/// <remarks>
/// A device is a face over an engine. Which engine used to be worked out from the device's id by
/// a list written into the application, so there could only ever be as many devices as there were
/// engines and one made in the designer under any other id was read off disc and silently never
/// reached the rack. The manifest says it now, and the old id list is kept only so that the eight
/// that shipped before this, whose ids are in every song and chain on anybody's disc, still work.
///
/// Which is exactly why this is worth a test. Those eight would go on working with the field left
/// empty, so nothing anybody could hear would say the shipped content had stopped being explicit,
/// and the first device to lean on the old list by accident would be the one nobody notices.
///
/// Shipped content goes wrong the way content goes wrong: an engine spelled differently from the
/// one the application knows is read, refused, and passed over in silence, and what somebody sees
/// is a device that is simply not on the rack. So this reads the folders with the same readers
/// the application uses rather than trusting them.
/// </remarks>
public class ShippedEngineTests
{
    /// <summary>The rack folder beside the program, found by walking up from the test's output.</summary>
    /// <param name="world">Which of the two folders, <c>machines</c> or <c>effects</c>.</param>
    private static string Shipped(string world)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", world))) at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, "rack", world);
    }

    /// <summary>
    /// The folders are found and are not empty.
    /// </summary>
    /// <remarks>
    /// Said out loud, because a test that quietly passes where its subject is missing reports
    /// nothing for the rest of its life.
    /// </remarks>
    [Theory]
    [InlineData("machines")]
    [InlineData("effects")]
    public void The_shipped_rack_is_where_it_should_be(string world)
    {
        string folder = Shipped(world);

        Assert.True(Directory.Exists(folder), "rack/" + world + " was not found above " + AppContext.BaseDirectory);
        Assert.NotEmpty(Directory.GetDirectories(folder));
    }

    /// <summary>Every shipped soundmachine names an engine, and one this build has.</summary>
    [Fact]
    public void Every_shipped_soundmachine_names_its_engine()
    {
        string folder = Shipped("machines");

        Assert.True(Directory.Exists(folder));

        foreach (string one in Directory.EnumerateDirectories(folder))
        {
            var project = SoundMachineProject.Open(one);

            Assert.True(project is not null, Path.GetFileName(one) + " could not be read");

            Assert.False(project!.Engine.Length == 0,
                project.Id + " names no engine, so it is leaning on the old id list");

            Assert.True(SoundMachine.EngineNamed(project.Engine) is not null,
                project.Id + " names the engine '" + project.Engine + "', which this build has not got");
        }
    }

    /// <summary>And every shipped effect does, read the same way.</summary>
    [Fact]
    public void Every_shipped_effect_names_its_engine()
    {
        string folder = Shipped("effects");

        Assert.True(Directory.Exists(folder));

        var engines = new SoundEffectEngines();

        foreach (string one in Directory.EnumerateDirectories(folder))
        {
            var project = SoundEffectProject.Open(one);

            Assert.True(project is not null, Path.GetFileName(one) + " could not be read");

            Assert.False(project!.Engine.Length == 0,
                project.Id + " names no engine, so it is leaning on the old id list");

            Assert.True(engines.HasEngine(project.Engine),
                project.Id + " names the engine '" + project.Engine + "', which this build has not got");
        }
    }

    /// <summary>
    /// And the engine each one names is the engine it used to be given by its id.
    /// </summary>
    /// <remarks>
    /// The half that matters to anybody's songs. Naming an engine is only safe if it names the
    /// same one the id implied: a shipped device that quietly moved to another engine would open
    /// every song that plays it and sound like something else.
    /// </remarks>
    [Fact]
    public void The_named_engine_is_the_one_the_id_used_to_imply()
    {
        foreach (string one in Directory.EnumerateDirectories(Shipped("machines")))
        {
            var project = SoundMachineProject.Open(one)!;

            SoundMachine.Forget();
            SoundMachine.Register(project.Id, project.Name, project.Summary, project.Theme, project.Engine);
            var named = SoundMachine.Installed.Single().Kind;

            SoundMachine.Forget();
            SoundMachine.Register(project.Id, project.Name, project.Summary, project.Theme, "");
            var implied = SoundMachine.Installed.Single().Kind;

            Assert.Equal(implied, named);
        }

        SoundMachine.Forget();

        var engines = new SoundEffectEngines();

        foreach (string one in Directory.EnumerateDirectories(Shipped("effects")))
        {
            var project = SoundEffectProject.Open(one)!;

            Assert.Equal(engines.EngineOf(project.Id, null), engines.EngineOf(project.Id, project.Engine),
                ignoreCase: true);
        }
    }
}
