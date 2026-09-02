using System.Linq;
using JingleBox2.Audio.Plugins;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// One of our effects on a track's chain: written into the song, and read back out of it.
/// </summary>
/// <remarks>
/// A chain holds two kinds of box now, and the file has to say which without breaking the ones
/// already on people's discs. That is the whole of what is worth testing here: what is written,
/// what comes back, and what happens to a chain saved by a version that has an effect this one
/// has never heard of.
/// </remarks>
public class SoundEffectChainTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>And the longest block, which our effects size themselves without.</summary>
    private const int Block = 512;

    /// <summary>A chain with one EchoBox on it, set to something other than its defaults.</summary>
    private static (PluginChain Chain, ISoundEffectEngine Engine) Made(bool bypassed = false)
    {
        var chain = new PluginChain();
        var engine = new SoundEffectEngines().Make(SoundEffectEngines.EchoBox, Rate, Block)!;

        engine.SetValue(Delay.Time, 250);
        engine.SetValue(Delay.Feedback, 0.6);
        engine.SetValue(Delay.Damp, 0.8);
        engine.SetValue(Delay.Mix, 0.45);

        chain.Add(engine).Bypassed = bypassed;

        return (chain, engine);
    }

    /// <summary>What is written down is the effect's id and what its knobs were at.</summary>
    /// <remarks>
    /// No path and no state lump: an effect of ours is not a file on this computer, it is a box
    /// this installation has registered, and everything it holds is in its parameters.
    /// </remarks>
    [Fact]
    public void One_of_ours_is_written_down_as_its_id_and_its_knobs()
    {
        var state = new PluginChainState();

        var written = state.Capture(Made().Chain);

        var saved = Assert.Single(written.Devices);

        Assert.Equal(SoundEffectEngines.EchoBox, saved.Effect);
        Assert.Equal("", saved.Path);
        Assert.Empty(saved.State);
        Assert.Equal(250, saved.Parameters[Delay.Time], 5);
        Assert.Equal(0.6, saved.Parameters[Delay.Feedback], 5);
        Assert.Equal(0.45, saved.Parameters[Delay.Mix], 5);
    }

    /// <summary>And it comes back the same, in the same place, switched off if it was.</summary>
    [Fact]
    public void And_comes_back_the_same()
    {
        var state = new PluginChainState();

        var written = state.Capture(Made(bypassed: true).Chain);

        var into = new PluginChain();

        Assert.Empty(state.Restore(into, written, Rate, Block));

        var device = Assert.Single(into.Slots);

        Assert.True(device.Bypassed);

        var engine = Assert.IsAssignableFrom<ISoundEffectEngine>(device.Insert);

        Assert.Equal(SoundEffectEngines.EchoBox, engine.Id);
        Assert.Equal(250, engine.ValueOf(Delay.Time), 5);
        Assert.Equal(0.6, engine.ValueOf(Delay.Feedback), 5);
        Assert.Equal(0.8, engine.ValueOf(Delay.Damp), 5);
        Assert.Equal(0.45, engine.ValueOf(Delay.Mix), 5);
    }

    /// <summary>
    /// An effect this build has no engine for is named rather than passed over in silence.
    /// </summary>
    /// <remarks>
    /// Which is a song written where somebody had an effect this installation has not: the rest
    /// of the chain still loads, and what is missing is said, the same as a missing plugin.
    /// </remarks>
    [Fact]
    public void An_effect_this_build_has_not_got_is_named()
    {
        var written = new PluginChainConfig();

        written.Devices.Add(new PluginSlotConfig { Effect = "effect.somebody-elses", Name = "Whatever" });

        var into = new PluginChain();

        Assert.Equal("effect.somebody-elses", Assert.Single(new PluginChainState().Restore(into, written, Rate, Block)));
        Assert.Equal(0, into.Count);
    }

    /// <summary>
    /// A chain saved before effects existed is read as what it was, which is plugins.
    /// </summary>
    /// <remarks>
    /// The field says which world a box came from and is absent from every chain already on
    /// somebody's disc, so absent has to mean plugin. Nothing here can load one, so what the
    /// test can say is that it went down the plugin road and was reported missing rather than
    /// being taken for one of ours.
    /// </remarks>
    [Fact]
    public void A_chain_from_before_effects_is_read_as_plugins()
    {
        var written = new PluginChainConfig();

        written.Devices.Add(new PluginSlotConfig { Id = "com.somebody.thing", Name = "Thing", Path = "/nowhere/thing.vst3" });

        var into = new PluginChain();

        Assert.Equal("Thing", Assert.Single(new PluginChainState().Restore(into, written, Rate, Block)));
    }

    /// <summary>The face writes straight into the engine, and says so once per real move.</summary>
    /// <remarks>
    /// A controller resting against an end sends the same number over and over, and every one of
    /// those would otherwise redraw the panel and reread the block in the chain.
    /// </remarks>
    [Fact]
    public void The_face_writes_into_the_engine()
    {
        var (_, engine) = Made();

        var values = new SoundEffectValues(engine);

        int said = 0;

        values.Said += _ => said++;

        values.Set(Delay.Mix, 0.9);
        values.Set(Delay.Mix, 0.9);

        Assert.Equal(0.9, engine.ValueOf(Delay.Mix), 5);
        Assert.Equal(0.9, values.Get(Delay.Mix), 5);
        Assert.Equal(1, said);
    }

    /// <summary>An engine says what it can be set to, so a chain can be saved without the face.</summary>
    [Fact]
    public void An_engine_says_what_it_can_be_set_to()
    {
        var engine = new SoundEffectEngines().Make(SoundEffectEngines.EchoBox, Rate, Block)!;

        Assert.Equal(
            new[] { Delay.Damp, Delay.Feedback, Delay.Mix, Delay.Time },
            engine.Keys.OrderBy(one => one, System.StringComparer.Ordinal).ToArray());
    }
}
