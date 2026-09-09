using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Reading every parameter of a plugin at once, which is the difference between writing a chain
/// down and standing still for a second while it is written.
/// </summary>
/// <remarks>
/// A chain is saved by walking every parameter, and that walk asked the plugin one at a time.
/// For a plugin in a process of its own each of those is a socket round trip, so a plugin with
/// five thousand parameters cost five thousand of them on every song save and every undo. It was
/// measured on a real session at just over a second, with a block of audio going past its own
/// budget in the middle of it.
///
/// So there are two halves to hold: the message that carries the lot has to survive being read
/// back, including from a process that fell over while sending it, and the walk on the contract
/// has to answer the same thing for a plugin that needs no message at all.
/// </remarks>
public class BridgeValuesTests
{
    /// <summary>A plugin that answers from a dictionary, so the walk can be asked without a process.</summary>
    private sealed class Bench : IPluginParameters
    {
        /// <summary>What it says its knobs are.</summary>
        private readonly List<PluginParameter> _parameters = new();

        /// <summary>Where each of them stands.</summary>
        private readonly Dictionary<uint, double> _values = new();

        /// <summary>How many times it was asked for one value on its own.</summary>
        public int Asked { get; private set; }

        /// <summary>Adds a knob standing at a value.</summary>
        public Bench With(uint id, double value)
        {
            _parameters.Add(new PluginParameter(id, "p" + id, 0, 1, 0, 0, false, false, false, true, ""));
            _values[id] = value;

            return this;
        }

        /// <inheritdoc/>
        public PluginInfo Info => new("bench", "Bench", "", "", "");

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() => _parameters;

        /// <inheritdoc/>
        public double ValueOf(uint id)
        {
            Asked++;

            return _values.TryGetValue(id, out double value) ? value : 0;
        }

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => "";

        /// <inheritdoc/>
        public void SetValue(uint id, double value) => _values[id] = value;

        /// <inheritdoc/>
        public event System.Action<uint, double>? Edited;

        /// <inheritdoc/>
        public event System.Action? Reloaded;

        /// <inheritdoc/>
        public byte[] SaveState() => System.Array.Empty<byte>();

        /// <inheritdoc/>
        public void LoadState(byte[]? state)
        {
            Edited?.Invoke(0, 0);
            Reloaded?.Invoke();
        }
    }

    /// <summary>
    /// A plugin loaded in this process has no crossing to save, so the walk is what it should do.
    /// </summary>
    [Fact]
    public void A_plugin_in_this_process_is_walked_one_parameter_at_a_time()
    {
        IPluginParameters plugin = new Bench().With(1, 0.25).With(2, 0.5).With(7, 1);

        var values = plugin.Values();

        Assert.Equal(3, values.Count);
        Assert.Equal(0.25, values[1]);
        Assert.Equal(0.5, values[2]);
        Assert.Equal(1, values[7]);
        Assert.Equal(3, ((Bench)plugin).Asked);
    }

    /// <summary>Every pair written comes back, exactly as it went in.</summary>
    [Fact]
    public void Every_value_survives_the_wire()
    {
        var body = new BridgeBody();

        var sent = new Dictionary<uint, double>();

        for (uint id = 0; id < 5037; id++) sent[id] = id / 5037.0;

        var back = body.ReadValues(body.Values(sent));

        Assert.Equal(sent.Count, back.Count);
        Assert.Equal(sent[0], back[0]);
        Assert.Equal(sent[2500], back[2500]);
        Assert.Equal(sent[5036], back[5036]);
    }

    /// <summary>
    /// A message cut off part way through keeps whatever was whole, and does not throw.
    /// </summary>
    /// <remarks>
    /// This is what a payload from a process that has just crashed looks like, and the bridge
    /// exists so that a plugin falling over takes nothing with it but itself.
    /// </remarks>
    [Fact]
    public void A_message_cut_short_keeps_what_was_whole()
    {
        var body = new BridgeBody();

        var sent = new Dictionary<uint, double> { [1] = 0.5, [2] = 0.75, [3] = 1 };

        var whole = body.Values(sent);
        var cut = new byte[whole.Length - 5];

        System.Array.Copy(whole, cut, cut.Length);

        var back = body.ReadValues(cut);

        Assert.Equal(2, back.Count);
        Assert.Equal(0.5, back[1]);
        Assert.Equal(0.75, back[2]);
    }

    /// <summary>
    /// A count off a damaged wire buys nothing, which is the fault this shape has had before.
    /// </summary>
    [Fact]
    public void A_damaged_count_buys_no_array()
    {
        var body = new BridgeBody();

        var damaged = new byte[] { 0xFF, 0xFF, 0xFF, 0x7F };

        Assert.Empty(body.ReadValues(damaged));
    }

    /// <summary>
    /// A plugin in this process is written one parameter at a time, which is the other half of
    /// the same walk.
    /// </summary>
    [Fact]
    public void A_plugin_in_this_process_is_written_one_parameter_at_a_time()
    {
        IPluginParameters plugin = new Bench().With(1, 0).With(2, 0);

        plugin.SetValues(new Dictionary<uint, double> { [1] = 0.3, [2] = 0.8 });

        var values = plugin.Values();

        Assert.Equal(0.3, values[1]);
        Assert.Equal(0.8, values[2]);
    }

    /// <summary>Nothing at all is an empty answer rather than a throw.</summary>
    [Fact]
    public void Nothing_reads_as_nothing()
    {
        var body = new BridgeBody();

        Assert.Empty(body.ReadValues(System.Array.Empty<byte>()));
        Assert.Empty(body.ReadValues(body.Values(new Dictionary<uint, double>())));
    }
}
