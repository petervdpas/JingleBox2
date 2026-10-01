using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Config;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The plugin cushion following what is recommended unless somebody chose one, including in a
/// settings file written before it could.
/// </summary>
/// <remarks>
/// Nought was the cushion every settings file was born with, so nought in a file from before this
/// version is a cushion nobody chose, and it is moved once onto following the recommendation.
/// A size somebody did choose is left exactly as it is, and from this version on nought is a
/// choice like any other, since a file can now say it follows instead.
/// </remarks>
public class CushionMigrationTests
{
    /// <summary>A store of its own, so tests cannot read each other's files.</summary>
    private static ConfigStore Store([System.Runtime.CompilerServices.CallerMemberName] string named = "") =>
        new("jinglebox2-cushion-" + named);

    /// <summary>Writes a settings file as it is and reads it back the way the application does.</summary>
    private static AppConfig Read(string json, [System.Runtime.CompilerServices.CallerMemberName] string named = "")
    {
        var store = Store(named);

        File.WriteAllText(store.ConfigPath, json);

        return store.LoadOrCreateDefault();
    }

    /// <summary>A cushion nobody chose, from before this version, follows the recommendation.</summary>
    [Fact]
    public void An_old_file_with_the_old_default_follows()
    {
        Assert.Equal(AppConfig.FollowsRecommendation, Read("{\"Version\":2,\"RenderAheadMs\":0}").RenderAheadMs);
    }

    /// <summary>A cushion somebody chose, from before this version, is left as it is.</summary>
    [Fact]
    public void An_old_file_with_a_chosen_cushion_keeps_it()
    {
        Assert.Equal(80, Read("{\"Version\":2,\"RenderAheadMs\":80}").RenderAheadMs);
    }

    /// <summary>From this version on, in step is a choice and is kept.</summary>
    [Fact]
    public void A_new_file_that_says_in_step_keeps_it()
    {
        Assert.Equal(0, Read("{\"Version\":" + AppConfig.CurrentVersion + ",\"RenderAheadMs\":0}").RenderAheadMs);
    }

    /// <summary>A file that does not mention the cushion follows the recommendation.</summary>
    [Fact]
    public void A_file_without_a_cushion_follows()
    {
        Assert.Equal(AppConfig.FollowsRecommendation, Read("{\"Version\":" + AppConfig.CurrentVersion + "}").RenderAheadMs);
    }

    /// <summary>A fresh installation follows the recommendation.</summary>
    [Fact]
    public void A_fresh_installation_follows()
    {
        var store = Store();

        if (File.Exists(store.ConfigPath)) File.Delete(store.ConfigPath);

        Assert.Equal(AppConfig.FollowsRecommendation, store.LoadOrCreateDefault().RenderAheadMs);
    }

    /// <summary>
    /// The move happens once: a file brought forward and then set to in step stays in step the
    /// next time it is read.
    /// </summary>
    [Fact]
    public void The_move_happens_once()
    {
        var store = Store();

        File.WriteAllText(store.ConfigPath, "{\"Version\":2,\"RenderAheadMs\":0}");

        var cfg = store.LoadOrCreateDefault();
        cfg.RenderAheadMs = 0;
        store.Save(cfg);

        Assert.Equal(0, store.LoadOrCreateDefault().RenderAheadMs);
    }

    /// <summary>Following resolves to what is recommended for what the system allows.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Following_is_the_recommended_cushion(bool realtime)
    {
        IAudioDefaults defaults = new AudioDefaults(new FixedRealtime(realtime));
        IAudioChoices choices = new AudioChoices();

        Assert.Equal(choices.RecommendedCushion(realtime), defaults.Cushion(AppConfig.FollowsRecommendation));
    }

    /// <summary>A chosen cushion is what runs, whatever is recommended.</summary>
    [Fact]
    public void A_chosen_cushion_is_what_runs()
    {
        IAudioDefaults defaults = new AudioDefaults(new FixedRealtime(true));

        Assert.Equal(0, defaults.Cushion(0));
        Assert.Equal(160, defaults.Cushion(160));
    }

    /// <summary>A system that answers the real-time question the way the test says.</summary>
    private sealed class FixedRealtime(bool allowed) : IRealtimeThread
    {
        /// <inheritdoc/>
        public bool Allowed => allowed;

        /// <inheritdoc/>
        public bool Take() => allowed;

        /// <inheritdoc/>
        public bool PossibleOn(bool linux) => linux;

        /// <inheritdoc/>
        public bool Possible => true;

        /// <inheritdoc/>
        public string Said() => allowed ? "real time" : "the ordinary scheduler";
    }
}
