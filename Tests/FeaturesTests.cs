using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Config;
using JingleBox2.Config.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which parts of the tracker and the mixer are switched on, said once and kept in the settings
/// as the words of the ones switched off.
/// </summary>
public class FeaturesTests
{
    /// <summary>Every part is described exactly once, under a word of its own.</summary>
    [Fact]
    public void Every_part_is_described_once()
    {
        var features = new Features();

        Assert.Equal(Enum.GetValues<Feature>().OrderBy(one => one), features.All.Select(one => one.Feature).OrderBy(one => one));
        Assert.Equal(features.All.Count, features.All.Select(one => one.Word).Distinct().Count());
        Assert.All(features.All, one => Assert.False(string.IsNullOrWhiteSpace(one.Name)));
        Assert.All(features.All, one => Assert.False(string.IsNullOrWhiteSpace(one.Hint)));
    }

    /// <summary>The tracker has four and the mixer three.</summary>
    [Fact]
    public void Each_page_has_its_own()
    {
        var features = new Features();

        Assert.Equal(4, features.On(FeaturePage.Tracker).Count);
        Assert.Equal(3, features.On(FeaturePage.Mixer).Count);
    }

    /// <summary>Nothing said means everything is on, which is every settings file before this.</summary>
    [Fact]
    public void Everything_is_on_to_begin_with()
    {
        var features = new Features(new AppConfig());

        Assert.All(Enum.GetValues<Feature>(), one => Assert.True(features.IsOn(one)));
    }

    /// <summary>Switching one off writes its word down, says the settings moved, and is said once.</summary>
    [Fact]
    public void Off_is_written_down_and_said()
    {
        var config = new AppConfig();
        int moved = 0;
        var told = new List<Feature>();
        var features = new Features(config, () => moved++);
        features.Changed += told.Add;

        features.Set(Feature.SideChain, false);
        features.Set(Feature.SideChain, false);

        Assert.False(features.IsOn(Feature.SideChain));
        Assert.Equal(["side-chain"], config.FeaturesOff);
        Assert.Equal(1, moved);
        Assert.Equal([Feature.SideChain], told);
    }

    /// <summary>Switching it back on takes the word out again.</summary>
    [Fact]
    public void On_takes_the_word_out()
    {
        var config = new AppConfig();
        var features = new Features(config);

        features.Set(Feature.Patchbay, false);
        features.Set(Feature.Patchbay, true);

        Assert.True(features.IsOn(Feature.Patchbay));
        Assert.Empty(config.FeaturesOff);
    }

    /// <summary>The words in a settings file are read back, and a word this build does not know is kept.</summary>
    [Fact]
    public void A_settings_file_is_read_and_strangers_are_kept()
    {
        var config = new AppConfig { FeaturesOff = ["pattern-automation", "from-a-later-build"] };
        var features = new Features(config);

        Assert.False(features.IsOn(Feature.PatternAutomation));
        Assert.True(features.IsOn(Feature.SongAutomation));

        features.Set(Feature.PatternAutomation, true);

        Assert.Equal(["from-a-later-build"], config.FeaturesOff);
    }

    /// <summary>A settings file with no list at all is everything on, not a fault.</summary>
    [Fact]
    public void No_list_is_everything_on()
    {
        var config = new AppConfig { FeaturesOff = null! };
        var features = new Features(config);

        features.Set(Feature.CommandEditor, false);

        Assert.Equal(["command-editor"], config.FeaturesOff);
    }

    /// <summary>A part this build does not have is not on, and switching it does nothing.</summary>
    [Fact]
    public void A_part_out_of_range_is_off_and_left_alone()
    {
        var features = new Features();

        Assert.False(features.IsOn((Feature)99));

        features.Set((Feature)99, false);
    }

    /// <summary>Automation is asked by which timeline the lane is on.</summary>
    [Fact]
    public void Automation_is_asked_by_timeline()
    {
        Config.Interfaces.IFeatures features = new Features();

        features.Set(Feature.SongAutomation, false);

        Assert.False(features.Automates(songWide: true));
        Assert.True(features.Automates(songWide: false));
    }

    /// <summary>A part switched off reaches the settings file as it is written.</summary>
    [Fact]
    public void Off_reaches_the_file()
    {
        var config = new AppConfig();
        var features = new Features(config);

        features.Set(Feature.SideChain, false);

        Assert.Contains("side-chain", new ConfigStore().Written(config));
    }
}
