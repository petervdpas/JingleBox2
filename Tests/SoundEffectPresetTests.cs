using System;
using System.IO;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A sound effect's presets: a folder of files, one preset to a file.
/// </summary>
/// <remarks>
/// An effect preset is a handful of numbers, which makes it the easiest thing on the rack to
/// write down and the easiest to get quietly wrong. What is checked here is mostly the unhappy
/// half: a folder that is not there, a file that is not JSON, a value naming a control the effect
/// has not got, a value past the end of its range, NaN, and a name a filesystem will not take.
/// Every one of those has to come back as a shelf somebody can still use, because a preset that
/// cannot be read is one preset and not the whole effect.
/// </remarks>
public class SoundEffectPresetTests : IDisposable
{
    /// <summary>A folder of its own, thrown away afterwards.</summary>
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "jb-effect-presets-" + Guid.NewGuid().ToString("N"));

    /// <summary>The shelf under test.</summary>
    private readonly SoundEffectPresets _shelf = new();

    /// <summary>An effect with four controls, which is EchoBox's shape.</summary>
    private readonly SoundEffectProject _effect;

    /// <summary>Makes the folder and the effect that lives in it.</summary>
    public SoundEffectPresetTests()
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
                new Parameter { Key = "feedback", Min = 0, Max = 0.95, Default = 0.35 },
                new Parameter { Key = "damp", Min = 0, Max = 1, Default = 0.3 },
                new Parameter { Key = "mix", Min = 0, Max = 1, Default = 0.3 }
            }
        };
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

    /// <summary>Where the files land, for the tests that write one by hand.</summary>
    private string Presets => Path.Combine(_home, SoundEffectProject.PresetsFolder);

    /// <summary>An effect with no presets folder offers none, and does not throw.</summary>
    [Fact]
    public void An_effect_with_no_folder_offers_nothing()
    {
        Assert.Empty(_shelf.For(_effect));
        Assert.Empty(_shelf.For(null));
        Assert.Empty(_shelf.For(new SoundEffectProject()));
    }

    /// <summary>One written and read back is the same preset.</summary>
    [Fact]
    public void One_written_comes_back_whole()
    {
        Assert.True(_shelf.Write(_effect, Made("Slapback", ("time", 120), ("feedback", 0.2)), 0));

        var read = _shelf.For(_effect);

        Assert.Single(read);
        Assert.Equal("Slapback", read[0].Name);
        Assert.Equal(120, read[0].Settings["time"]);
        Assert.Equal(0.2, read[0].Settings["feedback"]);
    }

    /// <summary>They come back in the order they were put in, by the number on the file.</summary>
    [Fact]
    public void They_come_back_in_the_order_they_were_written()
    {
        _shelf.Write(_effect, Made("Third", ("mix", 0.9)), 2);
        _shelf.Write(_effect, Made("First", ("mix", 0.1)), 0);
        _shelf.Write(_effect, Made("Second", ("mix", 0.5)), 1);

        Assert.Equal(new[] { "First", "Second", "Third" }, _shelf.For(_effect).Select(one => one.Name));
    }

    /// <summary>A key the effect has not got is dropped rather than carried.</summary>
    [Fact]
    public void A_setting_for_a_control_that_is_gone_is_dropped()
    {
        Directory.CreateDirectory(Presets);
        File.WriteAllText(Path.Combine(Presets, "00 Old.json"),
            """{ "Name": "Old", "time": 200, "wobble": 0.5 }""");

        var read = _shelf.For(_effect);

        Assert.Single(read);
        Assert.Equal(200, read[0].Settings["time"]);
        Assert.False(read[0].Settings.ContainsKey("wobble"));
    }

    /// <summary>A value past the end of its control is brought inside it.</summary>
    [Fact]
    public void A_value_past_the_end_is_brought_inside_it()
    {
        Directory.CreateDirectory(Presets);
        File.WriteAllText(Path.Combine(Presets, "00 Wild.json"),
            """{ "Name": "Wild", "time": 999999, "feedback": -3 }""");

        var read = _shelf.For(_effect);

        Assert.Equal(2000, read[0].Settings["time"]);
        Assert.Equal(0, read[0].Settings["feedback"]);
    }

    /// <summary>
    /// NaN is answered with where the control starts, never passed on.
    /// </summary>
    /// <remarks>
    /// <c>Math.Clamp</c> hands NaN back by design, which is how a patch off disc once left a
    /// voice silent for the whole of its life.
    /// </remarks>
    [Fact]
    public void Nonsense_becomes_where_the_control_starts()
    {
        Assert.True(_shelf.Write(_effect,
            Made("Broken", ("time", double.NaN), ("feedback", double.PositiveInfinity)), 0));

        var read = _shelf.For(_effect);

        Assert.Equal(375, read[0].Settings["time"]);
        Assert.Equal(0.35, read[0].Settings["feedback"]);
    }

    /// <summary>A file that is not JSON is one preset lost, not the shelf.</summary>
    [Fact]
    public void One_unreadable_file_is_one_preset()
    {
        _shelf.Write(_effect, Made("Good", ("mix", 0.4)), 0);

        File.WriteAllText(Path.Combine(Presets, "01 Broken.json"), "this is not json {{{");

        var read = _shelf.For(_effect);

        Assert.Single(read);
        Assert.Equal("Good", read[0].Name);
    }

    /// <summary>A file with no name inside it is called after the file.</summary>
    [Fact]
    public void A_file_with_no_name_is_called_after_itself()
    {
        Directory.CreateDirectory(Presets);
        File.WriteAllText(Path.Combine(Presets, "00 Nameless.json"), """{ "time": 400 }""");

        Assert.Equal("00 Nameless", _shelf.For(_effect)[0].Name);
    }

    /// <summary>A name a filesystem will not take is written all the same.</summary>
    [Fact]
    public void A_name_with_a_separator_in_it_still_writes()
    {
        Assert.True(_shelf.Write(_effect, Made("1/2 speed", ("time", 750)), 0));

        Assert.Equal("1/2 speed", _shelf.For(_effect)[0].Name);
    }

    /// <summary>A preset with no name at all is refused rather than written as a blank.</summary>
    [Fact]
    public void A_preset_with_no_name_is_refused()
    {
        Assert.False(_shelf.Write(_effect, Made("", ("time", 100)), 0));
        Assert.False(_shelf.Write(_effect, Made("   ", ("time", 100)), 0));
        Assert.False(_shelf.Write(null, Made("Nowhere", ("time", 100)), 0));

        Assert.Empty(_shelf.For(_effect));
    }

    /// <summary>Taking one off leaves the others where they were.</summary>
    [Fact]
    public void Taking_one_off_leaves_the_rest()
    {
        _shelf.Write(_effect, Made("Keep", ("mix", 0.1)), 0);
        _shelf.Write(_effect, Made("Drop", ("mix", 0.2)), 1);

        Assert.True(_shelf.Remove(_effect, "Drop"));
        Assert.Equal(new[] { "Keep" }, _shelf.For(_effect).Select(one => one.Name));
    }

    /// <summary>Taking off one that is not there says so rather than throwing.</summary>
    [Fact]
    public void Taking_off_one_that_is_not_there_says_so()
    {
        Assert.False(_shelf.Remove(_effect, "Nothing"));
        Assert.False(_shelf.Remove(_effect, ""));
        Assert.False(_shelf.Remove(null, "Anything"));
    }

    /// <summary>The file says which effect it is for, so one that travels can be placed.</summary>
    [Fact]
    public void The_file_says_which_effect_it_is_for()
    {
        _shelf.Write(_effect, Made("Named", ("mix", 0.6)), 0);

        string written = File.ReadAllText(Directory.EnumerateFiles(Presets).Single());

        Assert.Contains("\"Effect\": \"effect.echobox\"", written);
    }

    /// <summary>
    /// A value lands on the control's own grid, not between two of its steps.
    /// </summary>
    /// <remarks>
    /// A slider dragged across the page hands back a raw double, so a delay whose time moves in
    /// whole milliseconds was writing down 527.2144522144523. It is rounded where it is written
    /// rather than only where it is drawn, because the file is what somebody reads and what
    /// travels.
    /// </remarks>
    [Fact]
    public void A_value_lands_on_the_controls_own_grid()
    {
        _shelf.Write(_effect, Made("Dragged", ("time", 527.2144522144523), ("feedback", 0.417)), 0);

        var read = _shelf.For(_effect);

        Assert.Equal(527, read[0].Settings["time"]);
        Assert.Equal(0.42, read[0].Settings["feedback"]);
    }

    /// <summary>A control with no step keeps whatever it was handed.</summary>
    [Fact]
    public void A_control_with_no_step_keeps_the_number_it_was_given()
    {
        _effect.Parameters.Add(new Parameter { Key = "smooth", Min = 0, Max = 1, Step = 0, Default = 0.5 });

        _shelf.Write(_effect, Made("Fine", ("smooth", 0.123456)), 0);

        Assert.Equal(0.123456, _shelf.For(_effect)[0].Settings["smooth"]);
    }

    /// <summary>A preset, made in one line.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="settings">Where its controls stand.</param>
    private static SoundEffectPreset Made(string name, params (string Key, double Value)[] settings) =>
        new(name, settings.ToDictionary(one => one.Key, one => one.Value, StringComparer.Ordinal));
}
