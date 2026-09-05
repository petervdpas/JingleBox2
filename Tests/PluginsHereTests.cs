using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Finding the plugin on this machine that a song is asking for.
/// </summary>
/// <remarks>
/// A song wrote down where a plugin was, and where it was is the one thing about it that does not
/// travel. Both places a song names one, the instrument and the slot on a chain, handed the host
/// the stored path, so a song carried across found its plugins installed and scanned and asked
/// for a file that machine has never had.
///
/// The ids and the paths below are this repository's own test song rather than invented.
/// </remarks>
public class PluginsHereTests
{
    private readonly PluginsHere _here = new();

    private const string Linux = "/home/peter/.vst3/Serum2.vst3";
    private const string Windows = @"C:\Program Files\Common Files\VST3\Serum2.vst3";
    private const string SynthId = "56534558667350736572756D20320000";
    private const string EffectId = "56534558667351736572756D20322066";

    private static PluginInfo At(string id, string name, string path, PluginFormat format = PluginFormat.Vst3) =>
        new(id, name, "", "", path, format);

    private static IReadOnlyList<PluginInfo> Here() => new[]
    {
        At(SynthId, "Serum 2", Windows),
        At(EffectId, "Serum 2 FX", Windows)
    };

    /// <summary>Found by its id, at the path this machine keeps it at.</summary>
    [Fact]
    public void A_song_from_another_machine_finds_its_plugin()
    {
        Assert.Equal(Windows, _here.Same(At(SynthId, "Serum 2", Linux), Here()).Path);
    }

    /// <summary>
    /// Found by its name when the id does not match, which is the step that carries the load.
    /// </summary>
    /// <remarks>
    /// Whether a class id really is the same bytes on two platforms cannot be settled from one of
    /// them, so the name is there to catch it if it is not, and this is that case written down:
    /// the same plugin, an id that does not match, found anyway.
    /// </remarks>
    [Fact]
    public void A_plugin_whose_id_does_not_match_is_found_by_name()
    {
        Assert.Equal(Windows, _here.Same(At("something else", "Serum 2 FX", Linux), Here()).Path);
        Assert.Equal(EffectId, _here.Same(At("", "Serum 2 FX", Linux), Here()).Id);
    }

    /// <summary>
    /// Two plugins in one bundle are told apart, which the path alone cannot do.
    /// </summary>
    /// <remarks>
    /// The case that decides the order. These two share a file, so a song matched by path could
    /// be handed the synthesiser where it asked for the effect **on the machine it was saved on**,
    /// which has nothing to do with travelling.
    /// </remarks>
    [Fact]
    public void Two_plugins_in_one_bundle_are_told_apart()
    {
        Assert.Equal("Serum 2", _here.Same(At(SynthId, "Serum 2", Linux), Here()).Name);
        Assert.Equal("Serum 2 FX", _here.Same(At(EffectId, "Serum 2 FX", Linux), Here()).Name);
    }

    /// <summary>With neither id nor name, a path two plugins share decides nothing.</summary>
    [Fact]
    public void A_path_two_plugins_share_decides_nothing()
    {
        var asked = At("", "", Linux);

        Assert.Same(asked, _here.Same(asked, Here()));
    }

    /// <summary>Nothing empty ever matches anything else empty.</summary>
    /// <remarks>
    /// A song from before ids were written down has none, and two of those matching would hand
    /// back whichever plugin came first in the list, which is a different plugin playing the part
    /// with nothing anywhere saying so.
    /// </remarks>
    [Fact]
    public void Nothing_matches_on_emptiness()
    {
        var asked = At("", "", "");

        Assert.Same(asked, _here.Same(asked, new[] { At("", "", "") }));
    }

    /// <summary>The same plugin in the other format is a different plugin.</summary>
    /// <remarks>
    /// Two plugins here with two sets of parameter numbers, so answering one for the other would
    /// put a song's knob positions on the wrong control.
    /// </remarks>
    [Fact]
    public void A_clap_is_not_a_vst3()
    {
        var known = new[] { At("com.zamaudio.ZamDelay", "ZamDelay", "/usr/lib/clap/ZamDelay.clap", PluginFormat.Clap) };
        var asked = At("com.zamaudio.ZamDelay", "ZamDelay", "/nowhere.vst3");

        Assert.Same(asked, _here.Same(asked, known));
    }

    /// <summary>A plugin this machine has not got keeps its name, so it can be named.</summary>
    [Fact]
    public void A_plugin_that_is_not_here_is_still_named()
    {
        var asked = At("com.zamaudio.ZamDelay", "ZamDelay", "/usr/lib/clap/ZamDelay.clap", PluginFormat.Clap);

        Assert.Same(asked, _here.Same(asked, Here()));
        Assert.Same(asked, _here.Same(asked, null));
    }
}
