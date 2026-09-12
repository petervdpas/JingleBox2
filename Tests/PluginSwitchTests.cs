using System.Collections.Generic;
using System.IO;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Switching a folder off so it is not walked, and a plugin off so it is not offered.
/// </summary>
/// <remarks>
/// Two acts for two reasons. A folder is switched off to stop a scan walking it, since scanning is
/// the one place a plugin runs code before anybody has chosen to use it, so a folder holding
/// something that hangs costs every scan from now on. A plugin is switched off because it is
/// installed and unwanted in the pickers: the same plugin ships as a CLAP and a VST3, a bundle
/// holds an instrument and its effect twin, and two hundred rows is a list nobody can read.
///
/// Neither is a deletion, and that is most of what these say: what is switched off is still listed,
/// still says what it is, and is one tick from being back.
/// </remarks>
public sealed class PluginSwitchTests
{
    /// <summary>A plugin, by the two things that identify one: its id and where it lives.</summary>
    private static PluginInfo Plugin(string id, string path = "/usr/lib/clap/Thing.clap") =>
        new(id, "Thing", "Somebody", "1.0", path);

    /// <summary>Switches over lists a test can read afterwards.</summary>
    private static PluginSwitches Over(out List<string> places, out List<string> plugins, out int said)
    {
        var madePlaces = new List<string>();
        var madePlugins = new List<string>();
        int count = 0;

        var switches = new PluginSwitches(madePlaces, madePlugins, () => count++);

        places = madePlaces;
        plugins = madePlugins;
        said = count;

        return switches;
    }

    /// <summary>A folder switched off is not walked, and switching it back on undoes it.</summary>
    [Fact]
    public void A_folder_switched_off_is_not_walked()
    {
        var switches = Over(out var places, out _, out _);
        var place = new PluginPlace("/usr/lib/clap", PluginFormat.Clap);

        Assert.True(switches.Wanted(place));

        Assert.True(switches.Turn(place, on: false));
        Assert.False(switches.Wanted(place));
        Assert.Single(places);

        Assert.True(switches.Turn(place, on: true));
        Assert.True(switches.Wanted(place));
        Assert.Empty(places);
    }

    /// <summary>
    /// **The standard is half of what a place is**, so one path can be off for one and on for the
    /// other.
    /// </summary>
    /// <remarks>
    /// The case it exists for: a folder of your own is walked by both scanners, so a folder full
    /// of CLAPs is searched for VST3 bundles on every scan, finding nothing every time.
    /// </remarks>
    [Fact]
    public void One_folder_can_be_off_for_one_standard_and_on_for_the_other()
    {
        var switches = Over(out _, out _, out _);

        var clap = new PluginPlace("/home/me/plugins", PluginFormat.Clap);
        var vst3 = new PluginPlace("/home/me/plugins", PluginFormat.Vst3);

        switches.Turn(vst3, on: false);

        Assert.True(switches.Wanted(clap));
        Assert.False(switches.Wanted(vst3));
    }

    /// <summary>Turning one over twice the same way changes nothing and says so.</summary>
    /// <remarks>
    /// What a tick bound to a property does when the page is built: it is set to what it already
    /// is, and writing the settings out for that would mark them changed on every start.
    /// </remarks>
    [Fact]
    public void Turning_it_the_way_it_already_is_moves_nothing()
    {
        var switches = Over(out var places, out _, out _);
        var place = new PluginPlace("/usr/lib/vst3", PluginFormat.Vst3);

        Assert.False(switches.Turn(place, on: true));
        Assert.Empty(places);

        switches.Turn(place, on: false);

        Assert.False(switches.Turn(place, on: false));
        Assert.Single(places);
    }

    /// <summary>A line naming a standard this build has no word for is left exactly alone.</summary>
    /// <remarks>
    /// A folder switched off by a later version, and read as one of ours it would switch the wrong
    /// folder off. It is not offered to a scan either, since nothing here knows where to look.
    /// </remarks>
    [Fact]
    public void A_standard_this_build_has_no_word_for_is_left_alone()
    {
        var places = new List<string> { "lv2:/usr/lib/lv2", "clap:/usr/lib/clap" };

        var switches = new PluginSwitches(places);

        Assert.False(switches.Wanted(new PluginPlace("/usr/lib/clap", PluginFormat.Clap)));
        Assert.True(switches.Wanted(new PluginPlace("/usr/lib/lv2", PluginFormat.Clap)));

        var off = Assert.Single(switches.Off);

        Assert.Equal("/usr/lib/clap", off.Path);

        Assert.Equal(2, places.Count);
    }

    /// <summary>A plugin switched off is not offered, and switching it on undoes it.</summary>
    [Fact]
    public void A_plugin_switched_off_is_not_offered()
    {
        var switches = Over(out _, out var plugins, out _);
        var one = Plugin("abc");

        Assert.True(switches.Wanted(one));

        Assert.True(switches.Turn(one, on: false));
        Assert.False(switches.Wanted(one));
        Assert.Single(plugins);

        Assert.True(switches.Turn(one, on: true));
        Assert.True(switches.Wanted(one));
    }

    /// <summary>
    /// **By its id and never by its path**, since two classes in one bundle share a path.
    /// </summary>
    /// <remarks>
    /// This repository's own test song is the proof the rest of this half already keeps: Serum 2
    /// and Serum 2 FX have different class ids and the same path. Switched off by path, turning
    /// one off would take the other with it.
    /// </remarks>
    [Fact]
    public void Two_plugins_in_one_bundle_are_switched_apart()
    {
        var switches = Over(out _, out _, out _);

        var synth = Plugin("aaa", "/home/me/.vst3/Serum2.vst3");
        var effect = Plugin("bbb", "/home/me/.vst3/Serum2.vst3");

        switches.Turn(synth, on: false);

        Assert.False(switches.Wanted(synth));
        Assert.True(switches.Wanted(effect));
    }

    /// <summary>A plugin with no id at all is left offered rather than switched off by accident.</summary>
    [Fact]
    public void A_plugin_with_no_id_is_still_offered()
    {
        var switches = Over(out _, out _, out _);

        Assert.True(switches.Wanted(Plugin("")));
        Assert.False(switches.Turn(Plugin(""), on: false));
    }

    /// <summary>Nothing at all is asked of switches with nothing to ask about.</summary>
    [Fact]
    public void Nothing_switched_off_wants_everything()
    {
        var switches = new PluginSwitches();

        Assert.True(switches.Wanted(Plugin("abc")));
        Assert.True(switches.Wanted(new PluginPlace("/usr/lib/clap", PluginFormat.Clap)));
        Assert.Empty(switches.Off);
        Assert.True(switches.Wanted((PluginInfo?)null));
        Assert.True(switches.Wanted((PluginPlace?)null));
    }

    /// <summary>
    /// **A scan really leaves a switched-off folder alone**, which is the whole point of it.
    /// </summary>
    /// <remarks>
    /// Over a real folder with a real file in it, because everything above this is about a list of
    /// words and the thing that matters is a walk of a disc. The file is not a plugin and does not
    /// have to be: what is being asked is which folders were walked, and a bundle is opened long
    /// after this.
    /// </remarks>
    [Fact]
    public void A_scan_leaves_a_folder_that_is_switched_off_alone()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jinglebox-scan-" + Path.GetRandomFileName());

        Directory.CreateDirectory(folder);

        try
        {
            string bundle = Path.Combine(folder, "Thing.clap");

            File.WriteAllText(bundle, "not really a plugin");

            var scanner = new ClapScanner();

            Assert.Contains(bundle, scanner.Bundles(new[] { folder }));

            Assert.DoesNotContain(bundle, scanner.Bundles(new[] { folder }, new[] { folder }));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
