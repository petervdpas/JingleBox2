using System;
using System.IO;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The presets page for the effect open in the designer.
/// </summary>
/// <remarks>
/// Put a question to without a window, which is the whole reason the page is a view model with a
/// question for the effect rather than a control that reaches for one. What is checked is the
/// awkward half: pressing New twice, renaming onto a name that is taken, renaming to nothing, and
/// working on an effect that has never been saved, since every one of those is a way to lose a
/// preset somebody had made.
/// </remarks>
public class SoundEffectPresetDeskTests : IDisposable
{
    /// <summary>A folder of its own, thrown away afterwards.</summary>
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "jb-preset-desk-" + Guid.NewGuid().ToString("N"));

    /// <summary>The effect the page is about.</summary>
    private readonly SoundEffectProject _effect;

    /// <summary>Makes the folder and the effect in it.</summary>
    public SoundEffectPresetDeskTests()
    {
        Directory.CreateDirectory(_home);

        _effect = new SoundEffectProject
        {
            Id = "effect.echobox",
            Name = "EchoBox",
            Folder = _home,
            Parameters =
            {
                new Parameter { Key = "time", Name = "Time", Min = 10, Max = 2000, Default = 375 },
                new Parameter { Key = "mix", Name = "Mix", Min = 0, Max = 1, Default = 0.3 }
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

    /// <summary>A page over that effect.</summary>
    private SoundEffectPresetDesk Desk() => new(() => _effect);

    /// <summary>An effect never saved has nowhere to keep a preset, and says so.</summary>
    [Fact]
    public void An_effect_that_is_not_on_disc_is_not_ready()
    {
        var desk = new SoundEffectPresetDesk(() => new SoundEffectProject { Id = "effect.x" });

        Assert.False(desk.Ready);
        Assert.Empty(desk.Presets);

        desk.NewCommand.Execute(null);

        Assert.Empty(desk.Presets);
        Assert.True(desk.HasProblem);
    }

    /// <summary>New makes one, picks it, and fills the form from the face's own ends.</summary>
    [Fact]
    public void New_makes_one_and_picks_it()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);

        Assert.Single(desk.Presets);
        Assert.True(desk.HasPreset);
        Assert.Equal(desk.Picked, desk.Called);
        Assert.Equal(new[] { "Time", "Mix" }, desk.Settings.Select(one => one.Name));
        Assert.Equal(375, desk.Settings[0].Value);
    }

    /// <summary>Pressing New twice gives two presets rather than one overwritten.</summary>
    [Fact]
    public void New_twice_gives_two()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);
        desk.NewCommand.Execute(null);

        Assert.Equal(2, desk.Presets.Count);
        Assert.Equal(desk.Presets.Count, desk.Presets.Distinct().Count());
    }

    /// <summary>What is typed into the form is what comes back off the disc.</summary>
    [Fact]
    public void What_is_typed_is_what_is_kept()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);

        desk.Called = "Slapback";
        desk.Settings[0].Value = 120;
        desk.Settings[1].Value = 0.8;
        desk.SaveCommand.Execute(null);

        var read = new SoundEffectPresets().For(_effect);

        Assert.Single(read);
        Assert.Equal("Slapback", read[0].Name);
        Assert.Equal(120, read[0].Settings["time"]);
        Assert.Equal(0.8, read[0].Settings["mix"]);
    }

    /// <summary>A rename leaves one preset behind, not two.</summary>
    [Fact]
    public void A_rename_does_not_leave_the_old_one()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);
        desk.Called = "Wide";
        desk.SaveCommand.Execute(null);

        Assert.Single(desk.Presets);
        Assert.Equal("Wide", desk.Presets[0]);
        Assert.Equal("Wide", desk.Picked);
    }

    /// <summary>Renaming onto a name that is taken is refused, and neither is lost.</summary>
    [Fact]
    public void A_rename_onto_a_taken_name_is_refused()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);
        desk.Called = "One";
        desk.SaveCommand.Execute(null);

        desk.NewCommand.Execute(null);
        desk.Called = "One";
        desk.SaveCommand.Execute(null);

        Assert.True(desk.HasProblem);
        Assert.Equal(2, desk.Presets.Count);
        Assert.Contains("One", desk.Presets);
    }

    /// <summary>A preset renamed to nothing is refused rather than written as a blank.</summary>
    [Fact]
    public void A_rename_to_nothing_is_refused()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);

        string was = desk.Picked!;

        desk.Called = "   ";
        desk.SaveCommand.Execute(null);

        Assert.True(desk.HasProblem);
        Assert.Equal(new[] { was }, desk.Presets);
    }

    /// <summary>Delete takes the picked one off and moves on to whatever is left.</summary>
    [Fact]
    public void Delete_takes_the_picked_one_off()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);
        desk.Called = "Keep";
        desk.SaveCommand.Execute(null);

        desk.NewCommand.Execute(null);
        desk.Called = "Drop";
        desk.SaveCommand.Execute(null);

        desk.DeleteCommand.Execute(null);

        Assert.Equal(new[] { "Keep" }, desk.Presets);
        Assert.Equal("Keep", desk.Picked);
    }

    /// <summary>Delete with nothing picked does nothing rather than throwing.</summary>
    [Fact]
    public void Delete_with_nothing_picked_does_nothing()
    {
        var desk = Desk();

        desk.DeleteCommand.Execute(null);
        desk.SaveCommand.Execute(null);

        Assert.Empty(desk.Presets);
    }

    /// <summary>Picking another preset fills the form from that one.</summary>
    [Fact]
    public void Picking_another_fills_the_form_from_it()
    {
        var desk = Desk();

        desk.NewCommand.Execute(null);
        desk.Called = "Short";
        desk.Settings[0].Value = 60;
        desk.SaveCommand.Execute(null);

        desk.NewCommand.Execute(null);
        desk.Called = "Long";
        desk.Settings[0].Value = 900;
        desk.SaveCommand.Execute(null);

        desk.Picked = "Short";

        Assert.Equal(60, desk.Settings[0].Value);

        desk.Picked = "Long";

        Assert.Equal(900, desk.Settings[0].Value);
    }
}
