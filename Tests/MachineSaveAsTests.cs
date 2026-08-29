using System;
using System.IO;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Writing a machine into another folder, which is what Save as does.
/// </summary>
/// <remarks>
/// A machine is its folder, not its manifest: the manifest names pictures, presets and sounds
/// by the names they have inside it, so a manifest written on its own into an empty folder is a
/// machine that draws nothing and has no presets. That was the whole of what
/// <see cref="MachineProject.Save"/> did when given a folder, and it is why saving to another
/// place had to grow a second half rather than being a one-line command over the first.
/// </remarks>
public class MachineSaveAsTests : IDisposable
{
    private readonly IMachineArchive _crates = new MachineArchive();

    private readonly string _room =
        Path.Combine(Path.GetTempPath(), "jinglebox2-saveas-" + Guid.NewGuid().ToString("N"));

    /// <summary>A machine on the disc with a picture, a preset and a sound beside its manifest.</summary>
    private MachineProject Machine(string named)
    {
        string folder = Path.Combine(_room, named);

        var project = new MachineProject { Id = "machine.test", Name = named, Version = "1.00" };

        project.Save(folder);

        Directory.CreateDirectory(Path.Combine(folder, "images"));
        Directory.CreateDirectory(Path.Combine(folder, "presets"));
        Directory.CreateDirectory(Path.Combine(folder, "sounds"));

        File.WriteAllText(Path.Combine(folder, "images", "logo.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(folder, "presets", "one.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "sounds", "kick.wav"), "riff");

        return project;
    }

    /// <summary>Everything beside the manifest goes, which is the point of it existing.</summary>
    [Fact]
    public void The_whole_folder_travels()
    {
        var project = Machine("from");
        string into = Path.Combine(_room, "into");

        _crates.CopyInto(project, into);

        Assert.Equal("<svg/>", File.ReadAllText(Path.Combine(into, "images", "logo.svg")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(into, "presets", "one.json")));
        Assert.Equal("riff", File.ReadAllText(Path.Combine(into, "sounds", "kick.wav")));
    }

    /// <summary>
    /// The manifest is not carried, because the one on disc is behind whatever is on screen.
    /// The caller writes the current one afterwards, and copying a stale one first would only
    /// be overwriting it a moment later.
    /// </summary>
    [Fact]
    public void The_manifest_is_left_to_the_save_that_follows()
    {
        var project = Machine("from");
        string into = Path.Combine(_room, "into");

        _crates.CopyInto(project, into);

        Assert.False(File.Exists(Path.Combine(into, MachineProject.ManifestName)));

        project.Save(into);

        Assert.Equal(into, project.Folder);
        Assert.Equal("from", MachineProject.Open(into)!.Name);
    }

    /// <summary>
    /// The folder it came from is left exactly as it was, which is what makes this a copy and
    /// not a move, and what lets an edited machine be put back over the copy that ships without
    /// losing the one being worked on.
    /// </summary>
    [Fact]
    public void The_folder_it_came_from_is_untouched()
    {
        var project = Machine("from");
        string from = project.Folder;

        _crates.CopyInto(project, Path.Combine(_room, "into"));

        Assert.True(File.Exists(Path.Combine(from, "images", "logo.svg")));
        Assert.True(File.Exists(Path.Combine(from, MachineProject.ManifestName)));
    }

    /// <summary>
    /// Nothing in the destination is deleted, including a file this machine has not got. The
    /// registry's rule for a shipped machine being updated, and right for the same reason.
    /// </summary>
    [Fact]
    public void Nothing_in_the_destination_is_deleted()
    {
        var project = Machine("from");
        string into = Path.Combine(_room, "into");

        Directory.CreateDirectory(Path.Combine(into, "presets"));
        File.WriteAllText(Path.Combine(into, "presets", "theirs.json"), "kept");

        _crates.CopyInto(project, into);

        Assert.Equal("kept", File.ReadAllText(Path.Combine(into, "presets", "theirs.json")));
        Assert.True(File.Exists(Path.Combine(into, "presets", "one.json")));
    }

    /// <summary>A file that is already there is replaced, since this machine is what is being written.</summary>
    [Fact]
    public void A_file_of_its_own_is_written_over()
    {
        var project = Machine("from");
        string into = Path.Combine(_room, "into");

        Directory.CreateDirectory(Path.Combine(into, "images"));
        File.WriteAllText(Path.Combine(into, "images", "logo.svg"), "old");

        _crates.CopyInto(project, into);

        Assert.Equal("<svg/>", File.ReadAllText(Path.Combine(into, "images", "logo.svg")));
    }

    /// <summary>
    /// The folder it already lives in is nothing to do rather than an error. Somebody who picks
    /// it has asked for a save, and that is what the save after this gives them.
    /// </summary>
    [Fact]
    public void Its_own_folder_is_no_work_at_all()
    {
        var project = Machine("from");

        _crates.CopyInto(project, project.Folder);
        _crates.CopyInto(project, project.Folder + Path.DirectorySeparatorChar);

        Assert.True(File.Exists(Path.Combine(project.Folder, MachineProject.ManifestName)));
        Assert.Equal("<svg/>", File.ReadAllText(Path.Combine(project.Folder, "images", "logo.svg")));
    }

    /// <summary>
    /// A machine that has never been written down has no folder to carry, and says so rather
    /// than quietly writing a manifest into an empty folder.
    /// </summary>
    [Fact]
    public void A_machine_with_no_folder_is_refused()
    {
        var project = new MachineProject { Id = "machine.test", Name = "nowhere" };

        Assert.Throws<InvalidOperationException>(
            () => _crates.CopyInto(project, Path.Combine(_room, "into")));
    }

    /// <summary>And a destination that is not a place is refused before anything is made.</summary>
    [Fact]
    public void Nowhere_is_refused()
    {
        var project = Machine("from");

        Assert.Throws<ArgumentException>(() => _crates.CopyInto(project, "   "));
    }

    /// <summary>Takes the room down, whatever the tests left in it.</summary>
    public void Dispose()
    {
        try { if (Directory.Exists(_room)) Directory.Delete(_room, recursive: true); }
        catch (IOException) { }
    }
}
