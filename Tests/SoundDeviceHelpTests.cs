using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The page a device carries about itself: the file, the folder it travels in, and the line on
/// its Menu that opens it.
/// </summary>
/// <remarks>
/// Almost none of it is the happy path, deliberately. What a device's help has to survive is a
/// folder that is not there, a device that has never been saved, a page somebody emptied, and a
/// Menu asked about a device that is not open. Every one of those is a way to end up with a line
/// that opens nothing, which reads as the help being broken rather than as unwritten.
/// </remarks>
public class SoundDeviceHelpTests : IDisposable
{
    /// <summary>This test's own corner of the disc, thrown away afterwards.</summary>
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "jb2-help-" + Guid.NewGuid().ToString("N"));

    /// <summary>The rule under test, which is the same one the projects use.</summary>
    private readonly ISoundDeviceHelp _help = new SoundDeviceHelp();

    /// <summary>Takes the folder away.</summary>
    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (Exception) { }

        GC.SuppressFinalize(this);
    }

    /// <summary>A menu with nothing on it, so what the wrapper adds is what is counted.</summary>
    private sealed class Silent : IPanelMenu
    {
        /// <inheritdoc/>
        public IReadOnlyList<PanelMenuItem> Read() => Array.Empty<PanelMenuItem>();
    }

    /// <summary>A menu with one line on it, for saying where the page lands among them.</summary>
    private sealed class Says(string said) : IPanelMenu
    {
        /// <inheritdoc/>
        public IReadOnlyList<PanelMenuItem> Read() => new[] { new PanelMenuItem(said) };
    }

    /// <summary>An effect written into this test's folder, since a page lives beside a manifest.</summary>
    private SoundEffectProject Effect(string folder = "box", string help = "")
    {
        string where = Path.Combine(_root, folder);

        var made = new SoundEffectProject
        {
            Id = "effect.echo",
            Name = "EchoBox",
            Summary = "A delay.",
            Help = help
        };

        made.Save(where);

        return made;
    }

    /// <summary>A folder nobody has written a page into has none, and that is not a fault.</summary>
    [Fact]
    public void A_device_with_no_page_reads_as_nothing()
    {
        Directory.CreateDirectory(_root);

        Assert.Equal("", _help.Read(_root));
        Assert.Equal("", _help.Read(Path.Combine(_root, "not there")));
        Assert.Equal("", _help.Read(""));
        Assert.Equal("", _help.Read(null));
    }

    /// <summary>What is written is what comes back, whole, with its line breaks.</summary>
    [Fact]
    public void A_page_is_read_back_as_it_was_written()
    {
        Directory.CreateDirectory(_root);

        const string page = "# EchoBox\n\nTime is in milliseconds.\n\n- Feedback is how many\n";

        _help.Write(_root, page);

        Assert.True(File.Exists(Path.Combine(_root, _help.FileName)));
        Assert.Equal(page, _help.Read(_root));
    }

    /// <summary>Emptied means gone, since a file holding no words is a Menu line that opens nothing.</summary>
    [Fact]
    public void A_page_emptied_takes_its_file_with_it()
    {
        Directory.CreateDirectory(_root);

        _help.Write(_root, "Something.");
        _help.Write(_root, "");

        Assert.False(File.Exists(Path.Combine(_root, _help.FileName)));
        Assert.Equal("", _help.Read(_root));

        _help.Write(_root, "Something.");
        _help.Write(_root, "   ");

        Assert.False(File.Exists(Path.Combine(_root, _help.FileName)));
    }

    /// <summary>A device with no folder is written nowhere rather than throwing.</summary>
    [Fact]
    public void A_device_that_has_never_been_saved_is_left_alone()
    {
        _help.Write("", "Something.");
        _help.Write(null, "Something.");

        Assert.False(Directory.Exists(_root));
    }

    /// <summary>The page is part of saving a device, and part of reading one back.</summary>
    /// <remarks>
    /// The whole point of a file in the folder rather than a field in the manifest: what the zip
    /// carries, what Save as carries and what a shipped device is brought up to date with are all
    /// the folder, and none of them had to be told about this.
    /// </remarks>
    [Fact]
    public void Saving_a_device_writes_its_page_beside_the_manifest()
    {
        Effect(help: "# EchoBox\n\nWhat it does.\n");

        string where = Path.Combine(_root, "box");

        Assert.True(File.Exists(Path.Combine(where, _help.FileName)));

        var read = SoundEffectProject.Open(where);

        Assert.NotNull(read);
        Assert.Equal("# EchoBox\n\nWhat it does.\n", read!.Help);
    }

    /// <summary>A device saved with nothing to say has no page file at all.</summary>
    [Fact]
    public void Saving_a_device_with_nothing_to_say_writes_no_page()
    {
        Effect();

        Assert.False(File.Exists(Path.Combine(_root, "box", _help.FileName)));
        Assert.Equal("", SoundEffectProject.Open(Path.Combine(_root, "box"))!.Help);
    }

    /// <summary>The page is the first line of the Menu, before anything about hardware.</summary>
    [Fact]
    public void The_page_is_offered_first_and_opens_the_device()
    {
        var box = Effect(help: "Read me.");

        IRackProject? opened = null;

        var menu = new SoundDeviceMenu(new Says("A controller"), () => box, one => opened = one);

        var lines = menu.Read();

        Assert.Equal(2, lines.Count);
        Assert.Equal("Help", lines[0].Said);
        Assert.Equal(MenuOptionWords.Help, lines[0].Option);
        Assert.True(lines[0].Live);
        Assert.Equal("A controller", lines[1].Said);

        lines[0].Chosen!();

        Assert.Same(box, opened);
    }

    /// <summary>A device with no page keeps the line and loses the press.</summary>
    /// <remarks>
    /// A line that is not there says the host cannot show help; a line that is there and grey
    /// says this device's author wrote none, which is the truth and is also the nudge.
    /// </remarks>
    [Fact]
    public void A_device_with_no_page_greys_the_line_rather_than_dropping_it()
    {
        var box = Effect();

        bool opened = false;

        var lines = new SoundDeviceMenu(new Silent(), () => box, _ => opened = true).Read();

        var line = Assert.Single(lines);

        Assert.Equal("Help", line.Said);
        Assert.False(line.Live);
        Assert.Null(line.Chosen);
        Assert.Contains("no help page", line.Tip);
        Assert.False(opened);
    }

    /// <summary>With nothing open the line is still there, saying so.</summary>
    [Fact]
    public void With_no_device_open_the_line_says_there_is_nothing_to_read()
    {
        var lines = new SoundDeviceMenu(new Silent(), () => null).Read();

        var line = Assert.Single(lines);

        Assert.False(line.Live);
        Assert.Null(line.Chosen);
    }

    /// <summary>The device is asked for per press, since a panel is shown a different box as you work.</summary>
    [Fact]
    public void Which_device_the_page_is_about_is_asked_each_time()
    {
        var first = Effect("one", "First.");
        var second = Effect("two", "Second.");

        IRackProject? shown = first;

        IRackProject? opened = null;

        var menu = new SoundDeviceMenu(new Silent(), () => shown, one => opened = one);

        menu.Read()[0].Chosen!();
        Assert.Same(first, opened);

        shown = second;

        menu.Read()[0].Chosen!();
        Assert.Same(second, opened);
    }

    /// <summary>Every device's Menu can carry it, which is what puts it on the designer's ticks.</summary>
    [Fact]
    public void The_page_is_one_of_the_options_a_menu_can_be_given()
    {
        Assert.Contains(MenuOptionWords.Help, MenuOptionWords.All);
        Assert.Equal(MenuOptionWords.All.Count, MenuOptionWords.All.Distinct(StringComparer.Ordinal).Count());
    }
}
