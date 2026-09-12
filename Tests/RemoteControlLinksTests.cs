using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JingleBox2.Hints;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The links in a file of their own, and the writer that keeps it up.
/// </summary>
/// <remarks>
/// **The links are what is stored and the templates are what is read.** A template names the
/// controller as its profile calls it and never a port, deliberately, since the port is the part
/// that does not travel; so storing templates would mean settling a port again on every start.
/// What is kept here is the links as they stand, port and all.
///
/// They were a fifth of <c>config.json</c>, which is serialised whole whenever anything on any
/// page moves and read whole at startup before there is a window. Nothing about them is a
/// preference: they are written from the MIDI thread as a hand learns a knob.
/// </remarks>
public sealed class RemoteControlLinksTests : IDisposable
{
    /// <summary>A folder of this test's own, so one test cannot read another's file.</summary>
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "jinglebox2-links-" + Guid.NewGuid().ToString("N"));

    /// <summary>Points the store at it.</summary>
    private sealed class Here : JingleBox2.Files.Interfaces.IAppFolder
    {
        /// <summary>Where this one answers.</summary>
        private readonly string _path;

        /// <summary>Points it at a folder.</summary>
        /// <param name="path">The folder.</param>
        public Here(string path) => _path = path;

        /// <inheritdoc/>
        public string Name => "JingleBox2";

        /// <inheritdoc/>
        public string Path(string appName) => _path;

        /// <inheritdoc/>
        public string Path() => _path;
    }

    /// <summary>The store under test, over this test's own folder.</summary>
    private RemoteControlLinks Store() => new(new Here(_folder));

    /// <summary>A knob on a machine, learned on a port.</summary>
    private static ControlMapping Knob(string machine, int cc, string port = "nanoKONTROL2 _ CTRL") => new()
    {
        Device = port,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.SoundDevice,
        Machine = machine,
        Key = "knob" + cc,
        Owner = machine,
        Name = machine + " knob " + cc,
        Pickup = ControlPickup.Endless,
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Nothing written down is nothing pointed anywhere, which is a fresh installation.</summary>
    [Fact]
    public void Nothing_written_down_is_nothing_pointed_anywhere() => Assert.Empty(Store().Read());

    /// <summary>
    /// **What was kept comes back whole, the port among it.**
    /// </summary>
    /// <remarks>
    /// The port is the reason the links are what is stored rather than the templates: it is the
    /// one thing a template deliberately does not carry, and without it a link answers nothing
    /// until somebody settles it against this machine again.
    /// </remarks>
    [Fact]
    public void What_was_kept_comes_back_whole()
    {
        var store = Store();

        store.Keep(store.Written(new[] { Knob("machine.oddskilla", 16) }));

        var back = Assert.Single(store.Read());

        Assert.Equal("nanoKONTROL2 _ CTRL", back.Device);
        Assert.Equal(16, back.Cc);
        Assert.Equal("machine.oddskilla", back.Machine);
        Assert.Equal(ControlPickup.Endless, back.Pickup);
    }

    /// <summary>A write replaces what was there rather than adding to it.</summary>
    [Fact]
    public void A_write_replaces_what_was_there()
    {
        var store = Store();

        store.Keep(store.Written(new[] { Knob("machine.a", 16), Knob("machine.b", 17) }));
        store.Keep(store.Written(new[] { Knob("machine.a", 16) }));

        Assert.Single(store.Read());
    }

    /// <summary>A file that will not read is no links rather than a start that fails.</summary>
    /// <remarks>
    /// What is lost is a layout, which is a morning's work. What would be lost the other way is
    /// the application, since this is read before there is a window to report anything in.
    /// </remarks>
    [Fact]
    public void A_file_that_will_not_read_is_no_links()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, RemoteControlLinks.FileName), "{ not a list");

        Assert.Empty(Store().Read());
    }

    /// <summary>The first look writes, since what a previous run left cannot be known from here.</summary>
    [Fact]
    public void The_first_look_writes()
    {
        var store = Store();
        var block = new ControlLinkBlock(new List<ControlMapping> { Knob("machine.a", 16) });

        using var hints = new HintClock();

        var disc = new ControlLinksOnDisc(store, block, hints);

        Assert.True(disc.Check());
        Assert.Single(store.Read());
    }

    /// <summary>And a look at links that have not moved writes nothing.</summary>
    [Fact]
    public void A_look_at_links_that_have_not_moved_writes_nothing()
    {
        var store = Store();
        var block = new ControlLinkBlock(new List<ControlMapping> { Knob("machine.a", 16) });

        using var hints = new HintClock();

        var disc = new ControlLinksOnDisc(store, block, hints);

        disc.Check();

        Assert.False(disc.Check());
        Assert.False(disc.Check());
    }

    /// <summary>
    /// **A link that moved with nobody saying so is written all the same**, which is what the net
    /// is for.
    /// </summary>
    /// <remarks>
    /// The case it exists for is real and is not a gesture: the router works out what kind of
    /// control is sending and writes the pickup onto the mapping, from the MIDI thread.
    /// </remarks>
    [Fact]
    public void A_link_that_moved_unasked_is_still_written()
    {
        var store = Store();
        var block = new ControlLinkBlock(new List<ControlMapping> { Knob("machine.a", 16) });

        using var hints = new HintClock();

        var disc = new ControlLinksOnDisc(store, block, hints);

        disc.Check();

        block.Links[0].Pickup = ControlPickup.Jump;

        Assert.True(disc.Check());
        Assert.Equal(ControlPickup.Jump, Assert.Single(store.Read()).Pickup);
    }

    /// <summary>The way out writes what was left, since it cannot be waited through.</summary>
    [Fact]
    public void The_way_out_writes_what_was_left()
    {
        var store = Store();
        var block = new ControlLinkBlock();

        var hints = new HintClock();

        var disc = new ControlLinksOnDisc(store, block, hints);

        disc.Check();

        block.Links.Add(Knob("machine.c", 18));

        hints.Dispose();

        Assert.Single(store.Read());
    }

    /// <summary>And nothing is asked of it once it has been let go of.</summary>
    [Fact]
    public void Letting_it_go_stops_the_clock()
    {
        var store = Store();
        var block = new ControlLinkBlock();

        var hints = new HintClock();

        _ = new ControlLinksOnDisc(store, block, hints);

        hints.Dispose();

        string? was = File.Exists(store.Path()) ? File.ReadAllText(store.Path()) : null;

        block.Links.Add(Knob("machine.d", 19));
        block.Moved();

        Thread.Sleep(500);

        Assert.Equal(was, File.Exists(store.Path()) ? File.ReadAllText(store.Path()) : null);
    }
}
