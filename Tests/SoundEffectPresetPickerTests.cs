using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The picker on a sound effect's face, and what picking one actually does.
/// </summary>
/// <remarks>
/// The half that matters is where the values land. A preset written past the panel's own values
/// moves the sound and leaves every knob where it was, which reads from a chair as a preset that
/// did nothing rather than as a picture that is stale; this codebase has paid for that once
/// already, with a hardware knob on an effect. So what is checked is that picking writes through
/// <see cref="IPanelValues"/> and that a shelf which has shrunk under the picker cannot make it
/// reach past the end of its own list.
/// </remarks>
public class SoundEffectPresetPickerTests : IDisposable
{
    /// <summary>A folder of its own, thrown away afterwards.</summary>
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "jb-picker-" + Guid.NewGuid().ToString("N"));

    /// <summary>The effect the picker is on.</summary>
    private readonly SoundEffectProject _effect;

    /// <summary>Makes the folder, the effect, and two presets in it.</summary>
    public SoundEffectPresetPickerTests()
    {
        Directory.CreateDirectory(_home);

        _effect = new SoundEffectProject
        {
            Id = "effect.echobox",
            Name = "EchoBox",
            Folder = _home,
            Parameters =
            {
                new Parameter { Key = "time", Min = 10, Max = 2000, Step = 1, Default = 375 },
                new Parameter { Key = "mix", Min = 0, Max = 1, Step = 0.01, Default = 0.3 }
            }
        };

        var shelf = new SoundEffectPresets();

        shelf.Write(_effect, new SoundDevices.SoundEffects.Records.SoundEffectPreset(
            "Short", new Dictionary<string, double> { ["time"] = 90, ["mix"] = 0.2 }), 0);

        shelf.Write(_effect, new SoundDevices.SoundEffects.Records.SoundEffectPreset(
            "Long", new Dictionary<string, double> { ["time"] = 800, ["mix"] = 0.6 }), 1);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
        }
        catch (Exception)
        {
            // A folder the system is still holding is not this test's business.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>The picker offers what the folder holds, in its order.</summary>
    [Fact]
    public void The_picker_offers_what_the_folder_holds()
    {
        var picker = new SoundEffectPresetNames(_effect);

        Assert.Equal(new[] { "Short", "Long" }, picker.Names);
        Assert.Equal(-1, picker.Picked);
        Assert.Equal("Preset", picker.Caption);
    }

    /// <summary>An effect with no presets offers an empty picker rather than throwing.</summary>
    [Fact]
    public void An_effect_with_none_offers_an_empty_picker()
    {
        Assert.Empty(new SoundEffectPresetNames(null).Names);
        Assert.Empty(new SoundEffectPresetNames(new SoundEffectProject()).Names);
    }

    /// <summary>Picking one writes every setting it holds through the panel's values.</summary>
    [Fact]
    public void Picking_one_writes_through_the_panels_values()
    {
        var knobs = new Knobs();
        var picker = new SoundEffectPresetNames(_effect, knobs);

        picker.Picked = 1;

        Assert.Equal(800, knobs.Get("time"));
        Assert.Equal(0.6, knobs.Get("mix"));

        picker.Picked = 0;

        Assert.Equal(90, knobs.Get("time"));
        Assert.Equal(0.2, knobs.Get("mix"));
    }

    /// <summary>A picker with nowhere to write does nothing rather than throwing.</summary>
    [Fact]
    public void A_picker_with_no_values_does_nothing()
    {
        var picker = new SoundEffectPresetNames(_effect);

        picker.Picked = 1;

        Assert.Equal(1, picker.Picked);
    }

    /// <summary>
    /// A number outside the list is taken as none picked, and writes nothing.
    /// </summary>
    /// <remarks>
    /// A picker whose shelf has just shrunk hands one back, and reaching past the end of the list
    /// on the drawing thread would take the window with it.
    /// </remarks>
    [Fact]
    public void A_number_outside_the_list_is_none()
    {
        var knobs = new Knobs();
        var picker = new SoundEffectPresetNames(_effect, knobs);

        foreach (int wild in new[] { 2, 99, -1, -99, int.MaxValue, int.MinValue })
        {
            picker.Picked = wild;

            Assert.Equal(-1, picker.Picked);
        }

        Assert.Empty(knobs.Written);
    }

    /// <summary>Somewhere to read and write, standing in for a panel's own values.</summary>
    private sealed class Knobs : IPanelValues
    {
        /// <summary>What has been written into it, by key.</summary>
        public Dictionary<string, double> Written { get; } = new(StringComparer.Ordinal);

        /// <inheritdoc/>
        public event EventHandler<string>? Said;

        /// <inheritdoc/>
        public double Get(string key) => Written.TryGetValue(key, out double had) ? had : 0;

        /// <inheritdoc/>
        public void Set(string key, double value)
        {
            Written[key] = value;
            Said?.Invoke(this, key);
        }

        /// <inheritdoc/>
        public string GetText(string key) => "";

        /// <inheritdoc/>
        public void SetText(string key, string value) { }
    }
}
