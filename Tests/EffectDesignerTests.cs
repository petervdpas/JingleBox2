using System;
using System.IO;
using JingleBox2.Rack.Faces;
using JingleBox2.Devices.SoundEffects;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// One designer, two worlds: the page told which of the two it is laying out.
/// </summary>
/// <remarks>
/// The page itself is not tested here and does not need to be: dropping parts, sizing columns and
/// keeping the undo are the same work whichever world is open, and they were already tested for
/// machines. What is worth asking is everything that differs, which is what the world seam is:
/// what a fresh one is, what its id begins with, what the wording says, which pages are offered,
/// and whether a folder holding one kind can be opened as the other.
/// </remarks>
public class EffectDesignerTests : IDisposable
{
    /// <summary>This test's own corner of the disc, thrown away afterwards.</summary>
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "jb2-designer-" + Guid.NewGuid().ToString("N"));

    /// <summary>A folder under it, made on the way.</summary>
    private string Folder(string named)
    {
        string where = Path.Combine(_root, named);

        Directory.CreateDirectory(where);

        return where;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>The page told nothing is the machine designer, which is what it always was.</summary>
    [Fact]
    public void Told_nothing_it_designs_machines()
    {
        var designer = new DesignerViewModel();

        designer.NewCommand.Execute(null);

        Assert.Equal("machine", designer.Word);
        Assert.IsType<MachineProject>(designer.Project);
        Assert.StartsWith("machine.", designer.Project!.Id, StringComparison.Ordinal);
        Assert.True(designer.ShowsPresets);
        Assert.True(designer.ShowsExport);
    }

    /// <summary>And told the effect world, it designs effects and says so throughout.</summary>
    [Fact]
    public void Told_the_effect_world_it_designs_effects()
    {
        var designer = new DesignerViewModel(new EffectWorld());

        Assert.Equal("No effect open", designer.Title);

        designer.NewCommand.Execute(null);

        Assert.Equal("effect", designer.Word);
        Assert.IsType<EffectProject>(designer.Project);
        Assert.StartsWith("effect.", designer.Project!.Id, StringComparison.Ordinal);
        Assert.Equal("New effect", designer.Title);
        Assert.Equal("The effect", designer.Heading);
        Assert.Contains("effect", designer.Status, StringComparison.Ordinal);
    }

    /// <summary>
    /// An effect has no presets page and says so, and it does travel as a zip.
    /// </summary>
    /// <remarks>
    /// A machine's preset is an instrument file and an effect has no instrument, so that page
    /// would open on nothing. The zip is another matter: an effect is a folder with a manifest at
    /// the top of it, the same as a machine, so it is packed, handed over and imported by exactly
    /// the same code.
    /// </remarks>
    [Fact]
    public void An_effect_has_no_presets_page_and_does_have_a_zip()
    {
        var designer = new DesignerViewModel(new EffectWorld());

        designer.NewCommand.Execute(null);
        designer.Save(Folder("Echo"));

        Assert.False(designer.ShowsPresets);
        Assert.True(designer.ShowsExport);
        Assert.True(designer.CanExport);
    }

    /// <summary>What is laid out is written into the folder and read back the same.</summary>
    [Fact]
    public void An_effect_is_written_down_and_comes_back()
    {
        string where = Folder("Echo");

        var designer = new DesignerViewModel(new EffectWorld());

        designer.NewCommand.Execute(null);

        designer.Project!.Name = "Echo";
        designer.AddParameterCommand.Execute(null);
        designer.Drop(ElementKinds.Knob, designer.SelectedShape);
        designer.Save(where);

        string id = designer.Project.Id;

        var again = new DesignerViewModel(new EffectWorld());

        again.Open(where);

        Assert.NotNull(again.Project);
        Assert.Equal(id, again.Project!.Id);
        Assert.Equal("Echo", again.Project.Name);
        Assert.Single(again.Project.Parameters);
        Assert.Contains(again.Project.Panel.Root.Children, one => one.Element == ElementKinds.Knob);
    }

    /// <summary>A machine's folder is not an effect, and the page says so rather than opening it.</summary>
    [Fact]
    public void A_machine_folder_is_not_an_effect()
    {
        string where = Folder("Machine");

        var machines = new DesignerViewModel();

        machines.NewCommand.Execute(null);
        machines.Save(where);

        var effects = new DesignerViewModel(new EffectWorld());

        effects.Open(where);

        Assert.Null(effects.Project);
        Assert.Equal("No effect in " + where, effects.Status);
    }

    /// <summary>And the other way about, which is the same rule read from the other side.</summary>
    [Fact]
    public void An_effect_folder_is_not_a_machine()
    {
        string where = Folder("Effect");

        var effects = new DesignerViewModel(new EffectWorld());

        effects.NewCommand.Execute(null);
        effects.Save(where);

        var machines = new DesignerViewModel();

        machines.Open(where);

        Assert.Null(machines.Project);
        Assert.Equal("No machine in " + where, machines.Status);
    }

    /// <summary>Opening nothing at all says so and leaves the page as it was.</summary>
    [Fact]
    public void Opening_a_folder_with_nothing_in_it_says_so()
    {
        var designer = new DesignerViewModel(new EffectWorld());

        designer.Open(Folder("Empty"));

        Assert.Null(designer.Project);
    }

    /// <summary>
    /// Undo puts an effect back, which is the history reading a type it was never written for.
    /// </summary>
    /// <remarks>
    /// A step is the project's own JSON, and the reader used to name the machine's type on both
    /// sides of the trip. Asked of the project itself now, so a world added later needs nothing
    /// here, and this is what says so.
    /// </remarks>
    [Fact]
    public void Undo_puts_an_effect_back()
    {
        var designer = new DesignerViewModel(new EffectWorld());

        designer.NewCommand.Execute(null);
        designer.AddParameterCommand.Execute(null);

        Assert.Single(designer.Project!.Parameters);
        Assert.True(designer.History.CanUndo);

        designer.History.Undo(designer.Project);

        Assert.Empty(designer.Project.Parameters);
    }

    /// <summary>The two pages are two documents: opening one leaves the other where it was.</summary>
    [Fact]
    public void The_two_designers_hold_their_own_work()
    {
        var machines = new DesignerViewModel();
        var effects = new DesignerViewModel(new EffectWorld());

        machines.NewCommand.Execute(null);
        effects.NewCommand.Execute(null);

        machines.Project!.Name = "A machine";
        effects.Project!.Name = "An effect";

        Assert.Equal("A machine", machines.Title);
        Assert.Equal("An effect", effects.Title);
    }
}
